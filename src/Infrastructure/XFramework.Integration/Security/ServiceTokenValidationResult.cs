using System.Security.Claims;

namespace XFramework.Integration.Security;

public sealed record ServiceTokenValidationResult(
    bool IsValid,
    string? CallerClientId,
    string? Audience,
    IReadOnlySet<string> Scopes,
    ClaimsPrincipal? Principal,
    string? Error,
    int FailureStatusCode = 401)
{
    public static ServiceTokenValidationResult Failure(string error) =>
        new(false, null, null, new HashSet<string>(StringComparer.OrdinalIgnoreCase), null, error);

    public static ServiceTokenValidationResult Unavailable(string error) =>
        new(false, null, null, new HashSet<string>(StringComparer.OrdinalIgnoreCase), null, error, 503);
}
