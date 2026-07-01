using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace XFramework.Integration.Security;

public sealed class ServiceTokenValidator(
    IIdentitySigningKeyProvider signingKeyProvider,
    IOptions<ServiceIdentityOptions> options)
    : IServiceTokenValidator
{
    private static readonly JwtSecurityTokenHandler Handler = new();

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
        IReadOnlyList<SecurityKey> validationKeys;
        try
        {
            var keys = await signingKeyProvider.GetSigningKeysAsync(keyId, ct);
            validationKeys = keys
                .Where(static key => !string.IsNullOrWhiteSpace(key.PublicKeyPem))
                .Select(ToSecurityKey)
                .ToList();
        }
        catch (Exception ex)
        {
            return ServiceTokenValidationResult.Failure($"Service signing keys are unavailable: {ex.Message}");
        }

        if (validationKeys.Count == 0)
            return ServiceTokenValidationResult.Failure("No service signing key matched the token.");

        try
        {
            var principal = Handler.ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = validationKeys,
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

            var scopes = ExtractScopes(principal);
            if (requiredScopes is { Count: > 0 })
            {
                var missing = requiredScopes
                    .Where(scope => !scopes.Contains(scope))
                    .ToList();
                if (missing.Count > 0)
                {
                    return ServiceTokenValidationResult.Failure(
                        $"Service token is missing required scope(s): {string.Join(", ", missing)}.");
                }
            }

            return new ServiceTokenValidationResult(
                true,
                caller,
                expectedAudience,
                scopes,
                principal,
                null);
        }
        catch (Exception ex)
        {
            return ServiceTokenValidationResult.Failure($"Service token validation failed: {ex.Message}");
        }
    }

    private static SecurityKey ToSecurityKey(XFramework.Domain.Shared.ServiceIdentity.ServiceSigningKeyResponse key)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(key.PublicKeyPem);
        return new RsaSecurityKey(rsa)
        {
            KeyId = key.KeyId
        };
    }

    private static IReadOnlySet<string> ExtractScopes(ClaimsPrincipal principal)
    {
        var scopes = principal.Claims
            .Where(static claim => claim.Type is "scope" or "scp")
            .SelectMany(static claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return scopes;
    }
}
