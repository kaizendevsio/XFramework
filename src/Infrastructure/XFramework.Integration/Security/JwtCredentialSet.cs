using System.Text;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Integration.Security;

public static class JwtCredentialSet
{
    public const string GenerationClaim = "credential_generation";
    public const int MinimumHmacSha512SecretBytes = 64;

    public static void Validate(JwtOptions options, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ValidIssuer))
            throw new InvalidOperationException("JwtOptions:ValidIssuer is required.");

        if (string.IsNullOrWhiteSpace(options.ValidAudience))
            throw new InvalidOperationException("JwtOptions:ValidAudience is required.");

        CredentialGenerationValidator.Validate(
            nameof(JwtOptions),
            Current(options),
            Fallback(options),
            nowUtc,
            MinimumHmacSha512SecretBytes);
    }

    public static SymmetricSecurityKey CreateCurrentSigningKey(JwtOptions options) =>
        CreateSigningKey(Current(options));

    public static TokenValidationParameters CreateValidationParameters(
        JwtOptions options,
        bool validateLifetime,
        TimeProvider? timeProvider = null)
    {
        var clock = timeProvider ?? TimeProvider.System;
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (_, _, keyId, _) => ResolveValidationKeys(options, keyId, clock.GetUtcNow()),
            IssuerSigningKeyValidator = ValidateGenerationClaim,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = options.ValidAudience,
            ValidIssuer = options.ValidIssuer,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha512]
        };

        if (!validateLifetime)
        {
            parameters.LifetimeValidator = (notBefore, expires, _, validationParameters) =>
                ValidateRefreshLifetime(
                    notBefore,
                    expires,
                    clock.GetUtcNow().UtcDateTime,
                    validationParameters.ClockSkew);
        }

        return parameters;
    }

    public static IReadOnlyList<SecurityKey> ResolveValidationKeys(
        JwtOptions options,
        string? keyId,
        DateTimeOffset nowUtc)
    {
        List<SecurityKey> keys = [CreateCurrentSigningKey(options)];
        var fallback = Fallback(options);
        if (fallback is { } candidate && CredentialGenerationValidator.IsActive(candidate, nowUtc))
            keys.Add(CreateSigningKey(candidate));

        if (string.IsNullOrWhiteSpace(keyId))
            return keys;

        return keys
            .Where(key => string.Equals(key.KeyId, keyId, StringComparison.Ordinal))
            .ToList();
    }

    private static CredentialGenerationDescriptor Current(JwtOptions options) =>
        new(options.GenerationId, options.Secret);

    private static CredentialGenerationDescriptor? Fallback(JwtOptions options) =>
        !options.HasValidationFallback
            ? null
            : new CredentialGenerationDescriptor(
                options.ValidationFallback!.GenerationId,
                options.ValidationFallback.Secret,
                options.ValidationFallback.ValidUntilUtc);

    private static SymmetricSecurityKey CreateSigningKey(CredentialGenerationDescriptor generation) =>
        new(Encoding.UTF8.GetBytes(generation.Secret))
        {
            KeyId = generation.GenerationId.Trim()
        };

    private static bool ValidateGenerationClaim(
        SecurityKey signingKey,
        SecurityToken token,
        TokenValidationParameters _)
    {
        IEnumerable<Claim> claims = token switch
        {
            System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwtToken => jwtToken.Claims,
            JsonWebToken jsonWebToken => jsonWebToken.Claims,
            _ => []
        };
        var generationClaims = claims
            .Where(static claim => claim.Type == GenerationClaim)
            .Select(static claim => claim.Value)
            .ToList();

        // Tokens issued before the Phase 0 rollout did not carry generation metadata.
        if (generationClaims.Count == 0)
            return true;

        return generationClaims.Count == 1
            && !string.IsNullOrWhiteSpace(signingKey.KeyId)
            && string.Equals(generationClaims[0], signingKey.KeyId, StringComparison.Ordinal);
    }

    private static bool ValidateRefreshLifetime(
        DateTime? notBefore,
        DateTime? expires,
        DateTime nowUtc,
        TimeSpan clockSkew)
    {
        if (expires is null || notBefore > expires)
            return false;

        return notBefore is null || notBefore <= nowUtc.Add(clockSkew);
    }
}
