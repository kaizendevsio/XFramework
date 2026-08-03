using System.IdentityModel.Tokens.Jwt;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace XFramework.Integration.Security;

public sealed class ServiceTokenValidator(
    IIdentitySigningKeyProvider signingKeyProvider,
    IOptions<ServiceIdentityOptions> options,
    ILogger<ServiceTokenValidator> logger)
    : IServiceTokenValidator
{
    private const int MaxSuccessfulValidationCacheEntries = 1024;
    private static readonly JwtSecurityTokenHandler Handler = new();
    private readonly object _cacheLock = new();
    private readonly Dictionary<ValidationCacheKey, LinkedListNode<CachedValidation>> _successfulValidations = [];
    private readonly LinkedList<CachedValidation> _validationLru = [];

    public async Task<ServiceTokenValidationResult> ValidateAsync(
        string? token,
        string expectedAudience,
        IReadOnlyCollection<string>? requiredScopes = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return ServiceTokenValidationResult.Failure("Service access token is required.");

        if (string.IsNullOrWhiteSpace(expectedAudience))
            return ServiceTokenValidationResult.Failure("Expected service token audience is required.");

        var cacheKey = CreateCacheKey(token, expectedAudience);
        if (TryGetCachedValidation(cacheKey, out var cachedValidation))
            return ApplyCurrentPolicy(cachedValidation, requiredScopes);

        JwtSecurityToken unvalidated;
        try
        {
            unvalidated = Handler.ReadJwtToken(token);
        }
        catch
        {
            return ServiceTokenValidationResult.Failure("Service access token is malformed.");
        }

        var keyId = unvalidated.Header.Kid;
        List<ImportedSecurityKey> importedKeys = [];
        try
        {
            var keys = await signingKeyProvider.GetSigningKeysAsync(keyId, ct);
            foreach (var key in keys.Where(static key => !string.IsNullOrWhiteSpace(key.PublicKeyPem)))
                importedKeys.Add(ImportSecurityKey(key));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            DisposeImportedKeys(importedKeys);
            throw;
        }
        catch (Exception ex)
        {
            DisposeImportedKeys(importedKeys);
            logger.LogError(ex, "Service signing keys are unavailable.");
            return ServiceTokenValidationResult.Unavailable("Service signing keys are unavailable.");
        }

        if (importedKeys.Count == 0)
            return ServiceTokenValidationResult.Failure("No service signing key matched the token.");

        try
        {
            var principal = Handler.ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = importedKeys.Select(static key => key.SecurityKey),
                    ValidateIssuer = true,
                    ValidIssuer = options.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = expectedAudience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
                },
                out _);

            var caller = principal.FindFirst("client_id")?.Value
                ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrWhiteSpace(caller))
                return ServiceTokenValidationResult.Failure("Service token caller is missing.");

            var validation = new ServiceTokenValidationResult(
                true,
                caller,
                expectedAudience,
                ExtractScopes(principal),
                principal,
                null);
            var policyResult = ApplyCredentialGenerationPolicy(validation);
            if (!policyResult.IsValid)
                return policyResult;

            CacheSuccessfulValidation(cacheKey, validation, unvalidated.ValidTo);
            return ApplyRequiredScopes(validation, requiredScopes);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Service token validation failed.");
            return ServiceTokenValidationResult.Failure("Service token validation failed.");
        }
        finally
        {
            DisposeImportedKeys(importedKeys);
        }
    }

    private ServiceTokenValidationResult ApplyCurrentPolicy(
        ServiceTokenValidationResult validation,
        IReadOnlyCollection<string>? requiredScopes)
    {
        var generationResult = ApplyCredentialGenerationPolicy(validation);
        return generationResult.IsValid
            ? ApplyRequiredScopes(validation, requiredScopes)
            : generationResult;
    }

    private ServiceTokenValidationResult ApplyCredentialGenerationPolicy(
        ServiceTokenValidationResult validation)
    {
        var generationClaims = validation.Principal?
            .FindAll("client_credential_generation")
            .Select(static claim => claim.Value)
            .ToList() ?? [];
        if (generationClaims.Count != 1 || string.IsNullOrWhiteSpace(generationClaims[0]))
        {
            return ServiceTokenValidationResult.Failure(
                "Service token credential generation is not accepted.");
        }

        return validation;
    }

    private static ServiceTokenValidationResult ApplyRequiredScopes(
        ServiceTokenValidationResult validation,
        IReadOnlyCollection<string>? requiredScopes)
    {
        if (requiredScopes is not { Count: > 0 })
            return validation;

        var missing = requiredScopes
            .Where(scope => !validation.Scopes.Contains(scope))
            .ToList();
        return missing.Count == 0
            ? validation
            : ServiceTokenValidationResult.Failure(
                $"Service token is missing required scope(s): {string.Join(", ", missing)}.");
    }

    private bool TryGetCachedValidation(
        ValidationCacheKey key,
        out ServiceTokenValidationResult validation)
    {
        lock (_cacheLock)
        {
            if (!_successfulValidations.TryGetValue(key, out var node))
            {
                validation = default!;
                return false;
            }

            if (node.Value.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                _successfulValidations.Remove(key);
                _validationLru.Remove(node);
                validation = default!;
                return false;
            }

            _validationLru.Remove(node);
            _validationLru.AddFirst(node);
            validation = node.Value.Validation;
            return true;
        }
    }

    private void CacheSuccessfulValidation(
        ValidationCacheKey key,
        ServiceTokenValidationResult validation,
        DateTime validToUtc)
    {
        var expiresAtUtc = new DateTimeOffset(
            DateTime.SpecifyKind(validToUtc, DateTimeKind.Utc));
        if (expiresAtUtc <= DateTimeOffset.UtcNow)
            return;

        lock (_cacheLock)
        {
            if (_successfulValidations.TryGetValue(key, out var existing))
            {
                _validationLru.Remove(existing);
                _successfulValidations.Remove(key);
            }

            while (_successfulValidations.Count >= MaxSuccessfulValidationCacheEntries)
            {
                var oldest = _validationLru.Last;
                if (oldest is null)
                    break;
                _validationLru.RemoveLast();
                _successfulValidations.Remove(oldest.Value.Key);
            }

            var cached = new CachedValidation(key, validation, expiresAtUtc);
            var node = _validationLru.AddFirst(cached);
            _successfulValidations[key] = node;
        }
    }

    private static ValidationCacheKey CreateCacheKey(string token, string expectedAudience)
    {
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(MemoryMarshal.AsBytes(token.AsSpan()), digest);
        return new ValidationCacheKey(
            BinaryPrimitives.ReadUInt64LittleEndian(digest),
            BinaryPrimitives.ReadUInt64LittleEndian(digest[8..]),
            BinaryPrimitives.ReadUInt64LittleEndian(digest[16..]),
            BinaryPrimitives.ReadUInt64LittleEndian(digest[24..]),
            expectedAudience);
    }

    private static ImportedSecurityKey ImportSecurityKey(
        XFramework.Domain.Shared.ServiceIdentity.ServiceSigningKeyResponse key)
    {
        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(key.PublicKeyPem);
            if (rsa.KeySize < JwtCredentialSet.MinimumRsaKeySize)
            {
                throw new CryptographicException(
                    $"Service signing RSA keys must be at least {JwtCredentialSet.MinimumRsaKeySize} bits.");
            }

            return new ImportedSecurityKey(
                new RsaSecurityKey(rsa)
                {
                    KeyId = key.KeyId,
                    CryptoProviderFactory = new CryptoProviderFactory
                    {
                        CacheSignatureProviders = false
                    }
                },
                rsa);
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }

    private static void DisposeImportedKeys(IEnumerable<ImportedSecurityKey> keys)
    {
        foreach (var key in keys)
            key.Owner.Dispose();
    }

    private static IReadOnlySet<string> ExtractScopes(ClaimsPrincipal principal)
    {
        var scopes = principal.Claims
            .Where(static claim => claim.Type is "scope" or "scp")
            .SelectMany(static claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return scopes;
    }

    private readonly record struct ValidationCacheKey(
        ulong Part0,
        ulong Part1,
        ulong Part2,
        ulong Part3,
        string Audience);

    private sealed record CachedValidation(
        ValidationCacheKey Key,
        ServiceTokenValidationResult Validation,
        DateTimeOffset ExpiresAtUtc);

    private sealed record ImportedSecurityKey(SecurityKey SecurityKey, RSA Owner);
}
