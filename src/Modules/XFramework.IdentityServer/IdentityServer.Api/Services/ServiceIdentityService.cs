using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Services;

public sealed class ServiceIdentityService(
    IDataContext dataContext,
    IConfiguration configuration,
    ServiceIdentityConfiguration serviceIdentityConfiguration,
    JwtOptions jwtOptions,
    TimeProvider timeProvider,
    ILogger<ServiceIdentityService> logger,
    IHttpContextAccessor? httpContextAccessor = null,
    ITrustedServiceInvocationResolver? serviceInvocationResolver = null)
    : IServiceIdentityService
{
    private const string Algorithm = "RS256";
    private static readonly JwtSecurityTokenHandler TokenHandler = new();

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

        var requestedScopes = request.Scopes
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
        rsa.ImportFromPem(signingKey.PrivateKeyPem);

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
        List<Claim> claims =
        [
            new("client_id", client.ClientId),
            new("service", client.ClientId),
            new(JwtRegisteredClaimNames.Sub, client.ClientId),
            new("scope", XFrameworkServiceScopes.BoltService),
            new(JwtCredentialSet.GenerationClaim, jwtOptions.GenerationId.Trim()),
            new("client_credential_generation", clientCredentialGenerationId!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(issuedAt).ToString(), ClaimValueTypes.Integer64)
        ];

        var token = new JwtSecurityToken(
            issuer: jwtOptions.ValidIssuer,
            audience: jwtOptions.ValidAudience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(
                JwtCredentialSet.CreateCurrentSigningKey(jwtOptions),
                SecurityAlgorithms.HmacSha512));

        var accessToken = TokenHandler.WriteToken(token);
        logger.LogDebug(
            "Issued Bolt transport token. ClientId={ClientId} ClientCredentialGenerationId={ClientCredentialGenerationId} SigningGenerationId={SigningGenerationId} LifetimeSeconds={LifetimeSeconds}",
            client.ClientId,
            clientCredentialGenerationId,
            jwtOptions.GenerationId.Trim(),
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
        var query = dataContext.Query<ServiceSigningKey>()
            .Where(key => !key.RetiredAtUtc.HasValue || key.RetiredAtUtc > DateTime.UtcNow);

        if (!string.IsNullOrWhiteSpace(request.KeyId))
            query = query.Where(key => key.KeyId == request.KeyId.Trim());

        var keys = await query.ToListAsync(ct);
        if (keys.Count == 0 && string.IsNullOrWhiteSpace(request.KeyId))
        {
            var active = await RotateSigningKeyCoreAsync("auto-bootstrap", ct);
            keys = [active];
        }

        return Result<ServiceSigningKeysResponse>.Success(new ServiceSigningKeysResponse
        {
            Keys = keys
                .OrderByDescending(static key => key.IsActive)
                .ThenByDescending(static key => key.CreatedAtUtc)
                .Select(ToResponse)
                .ToList()
        });
    }

    public async Task<Result<ServiceSigningKeyResponse>> RotateSigningKeyAsync(
        RotateServiceSigningKeyRequest request,
        CancellationToken ct = default)
    {
        var adminResult = await EnsureSigningKeyAdminAsync(request.Metadata, ct);
        if (!adminResult.IsSuccess)
            return Result<ServiceSigningKeyResponse>.Failure(adminResult.Message!, adminResult.StatusCode);

        var key = await RotateSigningKeyCoreAsync(request.Reason ?? request.Metadata?.Name ?? "manual", ct);
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
            .Where(item => item.KeyId == request.KeyId)
            .FirstOrDefaultAsync(ct);

        if (key is null)
            return Result<ServiceSigningKeyResponse>.NotFound("Signing key not found");

        if (key.IsActive)
            return Result<ServiceSigningKeyResponse>.Failure("Active signing key cannot be retired before rotation", 400);

        key.RetiredAtUtc ??= DateTime.UtcNow;
        dataContext.Update(key);
        await dataContext.SaveChangesAsync(ct);

        return Result<ServiceSigningKeyResponse>.Success(ToResponse(key));
    }

    private async Task<ServiceSigningKey> GetOrCreateActiveSigningKeyAsync(CancellationToken ct)
    {
        var active = await dataContext.Query<ServiceSigningKey>()
            .Where(key => key.IsActive && !key.RetiredAtUtc.HasValue)
            .FirstOrDefaultAsync(ct);

        return active ?? await RotateSigningKeyCoreAsync("auto-bootstrap", ct);
    }

    private async Task<ServiceSigningKey> RotateSigningKeyCoreAsync(string createdBy, CancellationToken ct)
    {
        var currentKeys = await dataContext.Query<ServiceSigningKey>()
            .Where(key => key.IsActive)
            .ToListAsync(ct);

        foreach (var key in currentKeys)
        {
            key.IsActive = false;
            dataContext.Update(key);
        }

        using var rsa = RSA.Create(3072);
        var now = DateTime.UtcNow;
        var newKey = new ServiceSigningKey
        {
            Id = Guid.NewGuid(),
            KeyId = $"svc-{Guid.NewGuid():N}",
            Algorithm = Algorithm,
            PrivateKeyPem = rsa.ExportPkcs8PrivateKeyPem(),
            PublicKeyPem = rsa.ExportSubjectPublicKeyInfoPem(),
            CreatedAtUtc = now,
            ActivatedAtUtc = now,
            IsActive = true,
            CreatedBy = createdBy
        };

        dataContext.Add(newKey);
        await dataContext.SaveChangesAsync(ct);
        return newKey;
    }

    private string ResolveIssuer() =>
        serviceIdentityConfiguration.Issuer;

    private async Task<Result> EnsureSigningKeyAdminAsync(
        RequestMetadata? metadata,
        CancellationToken ct)
    {
        var user = httpContextAccessor?.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true && user.IsInRole("SuperAdmin"))
            return Result.Success();

        if (serviceInvocationResolver is not null)
        {
            var invocation = await serviceInvocationResolver.ResolveAsync(
                metadata,
                configuration["BoltConfiguration:ClientName"] ?? XFrameworkServiceNames.IdentityServer,
                [XFrameworkServiceScopes.IdentityAdmin],
                requireTenant: false,
                ct: ct);

            if (invocation.IsSuccess)
                return Result.Success();

            return Result.Failure(
                invocation.Error ?? "Trusted identity.admin service metadata is required for service signing key administration",
                invocation.StatusCode);
        }

        return Result.Forbidden("Service signing key administration requires SuperAdmin or trusted identity.admin service metadata");
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
