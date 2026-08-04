using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.IdentityModel.Tokens;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Services;

public sealed class ServiceIdentityService(
    IDataContext dataContext,
    ServiceIdentityConfiguration serviceIdentityConfiguration,
    IBoltTransportTokenSigner boltTransportTokenSigner,
    TimeProvider timeProvider,
    ILogger<ServiceIdentityService> logger,
    ITrustedInvocationContextAccessor? trustedInvocationContextAccessor = null,
    AppDbContext? appDbContext = null,
    IServiceSigningKeyStore? signingKeyStore = null)
    : IServiceIdentityService
{
    private const string Algorithm = "RS256";
    private const int MaxPublishedSigningKeys = 32;
    private const int SigningKeyMaintenanceBatchSize = 128;
    private static readonly JwtSecurityTokenHandler TokenHandler = new();
    private static readonly SemaphoreSlim SigningKeyRotationLock = new(1, 1);
    private readonly IServiceSigningKeyStore _signingKeyStore = signingKeyStore ??
        throw new InvalidOperationException(
            "A service signing-key store must be registered by the application composition root.");

    public async Task<Result<ServiceTokenResponse>> IssueTokenAsync(
        IssueServiceTokenRequest request,
        CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var client = serviceIdentityConfiguration.FindClient(request.ClientId);
        if (client is null || !client.TryAuthenticate(request.ClientSecret, now, out var credentialGenerationId))
            return Result<ServiceTokenResponse>.Unauthorized("Invalid service client credentials");

        if (!XFrameworkServiceNames.All.Contains(request.Audience))
            return Result<ServiceTokenResponse>.Failure("Unknown service token audience", 400);

        if (client.AllowedAudiences.Count > 0 && !client.AllowedAudiences.Contains(request.Audience))
            return Result<ServiceTokenResponse>.Forbidden("Service client is not allowed to request this audience");

        var requestedScopes = (request.Scopes ?? [])
            .Where(static scope => !string.IsNullOrWhiteSpace(scope))
            .Select(static scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requestedScopes.Count == 0)
            requestedScopes = client.AllowedScopes.ToList();

        var deniedScopes = requestedScopes
            .Where(scope => !client.AllowedScopes.Contains(scope))
            .ToList();
        if (deniedScopes.Count > 0)
            return Result<ServiceTokenResponse>.Forbidden($"Service client is not allowed scope(s): {string.Join(", ", deniedScopes)}");

        var signingKey = await GetOrCreateActiveSigningKeyAsync(ct);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(await _signingKeyStore.ReadPrivateKeyAsync(signingKey.PrivateKeyFileName, ct));

        var key = new RsaSecurityKey(rsa)
        {
            KeyId = signingKey.KeyId,
            CryptoProviderFactory = new CryptoProviderFactory
            {
                CacheSignatureProviders = false
            }
        };

        var issuedAt = timeProvider.GetUtcNow().UtcDateTime;
        var expires = issuedAt.AddMinutes(serviceIdentityConfiguration.TokenLifetimeMinutes);
        List<Claim> claims =
        [
            new("client_id", client.ClientId),
            new("client_credential_generation", credentialGenerationId!),
            new("scope", string.Join(' ', requestedScopes)),
            new(JwtRegisteredClaimNames.Sub, client.ClientId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(issuedAt).ToString(), ClaimValueTypes.Integer64)
        ];

        var token = new JwtSecurityToken(
            issuer: ResolveIssuer(),
            audience: request.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));

        logger.LogDebug(
            "Issued service token. ClientId={ClientId} ClientCredentialGenerationId={ClientCredentialGenerationId} Audience={Audience} KeyId={KeyId}",
            client.ClientId,
            credentialGenerationId,
            request.Audience,
            signingKey.KeyId);

        return Result<ServiceTokenResponse>.Success(new ServiceTokenResponse
        {
            AccessToken = TokenHandler.WriteToken(token),
            ExpiresAtUtc = expires,
            TokenType = "Bearer"
        });
    }

    public Task<Result<ServiceTokenResponse>> IssueBoltTransportTokenAsync(
        string? clientId,
        string? clientSecret,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!serviceIdentityConfiguration.BoltTransportTokenIssuerEnabled)
        {
            return Task.FromResult(
                Result<ServiceTokenResponse>.Failure("Bolt transport token issuance is disabled", 503));
        }

        var now = timeProvider.GetUtcNow();
        var client = serviceIdentityConfiguration.FindClient(clientId);
        if (client is null || !client.TryAuthenticate(clientSecret, now, out var clientCredentialGenerationId))
        {
            return Task.FromResult(
                Result<ServiceTokenResponse>.Unauthorized("Invalid service client credentials"));
        }

        if (!client.AllowedScopes.Contains(XFrameworkServiceScopes.BoltService))
        {
            return Task.FromResult(
                Result<ServiceTokenResponse>.Forbidden("Service client is not allowed to request Bolt transport access"));
        }

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds()).UtcDateTime;
        var expiresAt = issuedAt.AddSeconds(serviceIdentityConfiguration.BoltTransportTokenLifetimeSeconds);
        var accessToken = boltTransportTokenSigner.Sign(
            client.ClientId,
            clientCredentialGenerationId!,
            new DateTimeOffset(issuedAt, TimeSpan.Zero),
            new DateTimeOffset(expiresAt, TimeSpan.Zero));
        logger.LogDebug(
            "Issued Bolt transport token. ClientId={ClientId} ClientCredentialGenerationId={ClientCredentialGenerationId} KeyId={KeyId} LifetimeSeconds={LifetimeSeconds}",
            client.ClientId,
            clientCredentialGenerationId,
            boltTransportTokenSigner.KeyId,
            serviceIdentityConfiguration.BoltTransportTokenLifetimeSeconds);

        return Task.FromResult(Result<ServiceTokenResponse>.Success(new ServiceTokenResponse
        {
            AccessToken = accessToken,
            ExpiresAtUtc = expiresAt,
            TokenType = "Bearer"
        }));
    }

    public async Task<Result<ServiceSigningKeysResponse>> GetSigningKeysAsync(
        GetServiceSigningKeysRequest request,
        CancellationToken ct = default)
    {
        await CleanupExpiredSigningKeysAsync(ct);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var query = dataContext.Query<ServiceSigningKey>()
            .Where(key =>
                key.IsActive ||
                key.RetiredAtUtc > now ||
                !key.RetiredAtUtc.HasValue);

        List<ServiceSigningKey> keys;
        if (!string.IsNullOrWhiteSpace(request.KeyId))
        {
            keys = await query
                .Where(key => key.KeyId == request.KeyId.Trim())
                .Take(1)
                .ToListAsync(ct);
        }
        else
        {
            keys = await query
                .OrderByDescending(key => key.IsActive)
                .ThenByDescending(key => key.CreatedAtUtc)
                .Take(MaxPublishedSigningKeys)
                .ToListAsync(ct);
        }

        if (keys.Count == 0 && string.IsNullOrWhiteSpace(request.KeyId))
        {
            var active = await RotateSigningKeyCoreAsync("auto-bootstrap", reuseActiveKey: true, ct);
            keys = [active];
        }

        return Result<ServiceSigningKeysResponse>.Success(new ServiceSigningKeysResponse
        {
            Keys = keys
                .OrderByDescending(static key => key.IsActive)
                .ThenByDescending(static key => key.CreatedAtUtc)
                .Select(ToResponse)
                .ToList(),
            CredentialGenerationsByClient = serviceIdentityConfiguration.ValidationGenerationIdsByClient
                .ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value.ToList(),
                    StringComparer.Ordinal)
        });
    }

    public async Task<Result<ServiceSigningKeyResponse>> RotateSigningKeyAsync(
        RotateServiceSigningKeyRequest request,
        CancellationToken ct = default)
    {
        var adminResult = await EnsureSigningKeyAdminAsync(request.Metadata, ct);
        if (!adminResult.IsSuccess)
            return Result<ServiceSigningKeyResponse>.Failure(adminResult.Message!, adminResult.StatusCode);

        var key = await RotateSigningKeyCoreAsync(
            request.Reason?.Trim() ?? request.Metadata?.OperationName?.Trim() ?? "manual",
            reuseActiveKey: false,
            ct);
        return Result<ServiceSigningKeyResponse>.Success(ToResponse(key));
    }

    public async Task<Result<ServiceSigningKeyResponse>> RetireSigningKeyAsync(
        RetireServiceSigningKeyRequest request,
        CancellationToken ct = default)
    {
        var adminResult = await EnsureSigningKeyAdminAsync(request.Metadata, ct);
        if (!adminResult.IsSuccess)
            return Result<ServiceSigningKeyResponse>.Failure(adminResult.Message!, adminResult.StatusCode);

        if (string.IsNullOrWhiteSpace(request.KeyId))
            return Result<ServiceSigningKeyResponse>.Failure("KeyId is required", 400);

        var key = await dataContext.Query<ServiceSigningKey>()
            .Where(item => item.KeyId == request.KeyId.Trim())
            .FirstOrDefaultAsync(ct);

        if (key is null)
            return Result<ServiceSigningKeyResponse>.NotFound("Signing key not found");

        if (key.IsActive)
            return Result<ServiceSigningKeyResponse>.Failure("Active signing key cannot be retired before rotation", 400);

        key.RetiredAtUtc ??= timeProvider.GetUtcNow().UtcDateTime;
        dataContext.Update(key);
        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<ServiceSigningKeyResponse>.Failure("Signing key could not be retired", saveResult.StatusCode);

        return Result<ServiceSigningKeyResponse>.Success(ToResponse(key));
    }

    private async Task<ServiceSigningKey> GetOrCreateActiveSigningKeyAsync(CancellationToken ct)
    {
        var active = await dataContext.Query<ServiceSigningKey>()
            .Where(key => key.IsActive && !key.RetiredAtUtc.HasValue)
            .FirstOrDefaultAsync(ct);

        return active ?? await RotateSigningKeyCoreAsync("auto-bootstrap", reuseActiveKey: true, ct);
    }

    private async Task<ServiceSigningKey> RotateSigningKeyCoreAsync(
        string createdBy,
        bool reuseActiveKey,
        CancellationToken ct)
    {
        await SigningKeyRotationLock.WaitAsync(ct);
        string? createdPrivateKeyReference = null;
        try
        {
            await using var transaction = appDbContext is not null && appDbContext.Database.IsRelational()
                ? await appDbContext.Database.BeginTransactionAsync(ct)
                : null;
            if (transaction is not null)
            {
                await appDbContext!.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock(hashtextextended('identity:service-signing-key-rotation', 0))",
                    ct);
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            await CleanupExpiredSigningKeysCoreAsync(now, ct);

            if (reuseActiveKey)
            {
                var activeKey = await dataContext.Query<ServiceSigningKey>()
                    .Where(key => key.IsActive && !key.RetiredAtUtc.HasValue)
                    .OrderByDescending(key => key.ActivatedAtUtc)
                    .FirstOrDefaultAsync(ct);
                if (activeKey is not null)
                {
                    if (transaction is not null)
                        await transaction.CommitAsync(ct);
                    return activeKey;
                }
            }

            var publishedKeys = await dataContext.Query<ServiceSigningKey>()
                .Where(key =>
                    key.IsActive ||
                    key.RetiredAtUtc > now ||
                    !key.RetiredAtUtc.HasValue)
                .Take(MaxPublishedSigningKeys)
                .ToListAsync(ct);
            if (publishedKeys.Count >= MaxPublishedSigningKeys)
            {
                throw new InvalidOperationException(
                    "Service signing key rotation is temporarily unavailable because the live key window is full.");
            }

            var currentKeys = await dataContext.Query<ServiceSigningKey>()
                .Where(key => key.IsActive)
                .ToListAsync(ct);

            foreach (var key in currentKeys)
            {
                key.IsActive = false;
                key.RetiredAtUtc = now.Add(GetSigningKeyRetirementOverlap());
                dataContext.Update(key);
            }

            using var rsa = RSA.Create(3072);
            var keyId = $"svc-{Guid.NewGuid():N}";
            var keyReference = await _signingKeyStore.StorePrivateKeyAsync(
                keyId,
                rsa.ExportPkcs8PrivateKeyPem(),
                ct);
            createdPrivateKeyReference = keyReference;
            var newKey = new ServiceSigningKey
            {
                Id = Guid.NewGuid(),
                KeyId = keyId,
                Algorithm = Algorithm,
                PrivateKeyFileName = keyReference,
                PublicKeyPem = rsa.ExportSubjectPublicKeyInfoPem(),
                CreatedAtUtc = now,
                ActivatedAtUtc = now,
                IsActive = true,
                CreatedBy = createdBy
            };

            dataContext.Add(newKey);
            var saveResult = await dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                throw new InvalidOperationException("Service signing key rotation could not be persisted.");

            if (transaction is not null)
                await transaction.CommitAsync(ct);

            createdPrivateKeyReference = null;
            return newKey;
        }
        catch
        {
            if (createdPrivateKeyReference is not null)
                await _signingKeyStore.DeletePrivateKeyAsync(createdPrivateKeyReference, CancellationToken.None);
            throw;
        }
        finally
        {
            SigningKeyRotationLock.Release();
        }
    }

    private TimeSpan GetSigningKeyRetirementOverlap() =>
        TimeSpan.FromMinutes(serviceIdentityConfiguration.TokenLifetimeMinutes);

    private async Task CleanupExpiredSigningKeysAsync(CancellationToken ct)
    {
        await SigningKeyRotationLock.WaitAsync(ct);
        try
        {
            await CleanupExpiredSigningKeysCoreAsync(timeProvider.GetUtcNow().UtcDateTime, ct);
        }
        finally
        {
            SigningKeyRotationLock.Release();
        }
    }

    private async Task CleanupExpiredSigningKeysCoreAsync(DateTime now, CancellationToken ct)
    {
        var expiredKeys = await dataContext.Query<ServiceSigningKey>()
            .Where(key => !key.IsActive && key.RetiredAtUtc.HasValue && key.RetiredAtUtc <= now)
            .OrderBy(key => key.RetiredAtUtc)
            .Take(SigningKeyMaintenanceBatchSize)
            .ToListAsync(ct);

        var remainingCapacity = SigningKeyMaintenanceBatchSize - expiredKeys.Count;
        var legacyKeys = remainingCapacity > 0
            ? await dataContext.Query<ServiceSigningKey>()
                .Where(key => !key.IsActive && !key.RetiredAtUtc.HasValue)
                .OrderBy(key => key.CreatedAtUtc)
                .Take(remainingCapacity)
                .ToListAsync(ct)
            : [];

        var changed = false;
        foreach (var key in expiredKeys.Concat(legacyKeys).DistinctBy(key => key.Id))
        {
            if (!key.RetiredAtUtc.HasValue)
            {
                key.RetiredAtUtc = now.Add(GetSigningKeyRetirementOverlap());
                dataContext.Update(key);
                changed = true;
            }

            if (key.RetiredAtUtc > now || !await TryDeletePrivateKeyAsync(key, ct))
                continue;

            dataContext.Remove(key);
            changed = true;
        }

        if (!changed)
            return;

        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            throw new InvalidOperationException("Expired service signing keys could not be cleaned up.");
    }

    private async Task<bool> TryDeletePrivateKeyAsync(ServiceSigningKey key, CancellationToken ct)
    {
        try
        {
            await _signingKeyStore.DeletePrivateKeyAsync(key.PrivateKeyFileName, ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Expired service signing key file could not be deleted. KeyId={KeyId}",
                key.KeyId);
            return false;
        }
    }

    private string ResolveIssuer() =>
        serviceIdentityConfiguration.Issuer;

    private Task<Result> EnsureSigningKeyAdminAsync(
        RequestMetadata? metadata,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var context = trustedInvocationContextAccessor?.Current;
        if (context?.Actor is { } actor)
        {
            return Task.FromResult(actor.Roles.Contains("SuperAdmin")
                ? Result.Success()
                : Result.Forbidden("Service signing key administration requires a SuperAdmin actor"));
        }

        if (context?.Service?.Scopes.Contains(XFrameworkServiceScopes.IdentityAdmin) == true)
        {
            return Task.FromResult(Result.Success());
        }

        return Task.FromResult(
            Result.Forbidden("Service signing key administration requires SuperAdmin or a trusted identity.admin service identity"));
    }

    private static ServiceSigningKeyResponse ToResponse(ServiceSigningKey key) => new()
    {
        KeyId = key.KeyId,
        Algorithm = key.Algorithm,
        PublicKeyPem = key.PublicKeyPem,
        CreatedAtUtc = key.CreatedAtUtc,
        ActivatedAtUtc = key.ActivatedAtUtc,
        RetiredAtUtc = key.RetiredAtUtc,
        IsActive = key.IsActive
    };

}
