using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace XFramework.Core.RateLimiting;

public readonly record struct StrictSecurityRateLimitPolicy(
    string Name,
    int PermitLimit,
    TimeSpan Window);

public static class StrictSecurityRateLimitPolicyMap
{
    public static readonly StrictSecurityRateLimitPolicy Authentication =
        new("auth", 10, TimeSpan.FromMinutes(1));

    public static readonly StrictSecurityRateLimitPolicy Refresh =
        new("refresh", 10, TimeSpan.FromMinutes(1));

    private static readonly StrictSecurityRateLimitPolicy ServiceIdentity =
        new("service-identity", 5, TimeSpan.FromMinutes(1));

    public static readonly StrictSecurityRateLimitPolicy PasswordReset =
        new("password-reset", 3, TimeSpan.FromMinutes(15));

    public static readonly StrictSecurityRateLimitPolicy Verification =
        new("verification", 5, TimeSpan.FromMinutes(15));

    public static bool TryResolve(HttpRequest request, out StrictSecurityRateLimitPolicy policy)
    {
        if (HttpMethods.IsPost(request.Method)
            && (request.Path.Equals("/api/service-identity/token", StringComparison.OrdinalIgnoreCase)
                || request.Path.Equals("/api/service-identity/bolt-transport-token", StringComparison.OrdinalIgnoreCase)))
        {
            policy = ServiceIdentity;
            return true;
        }

        if (HttpMethods.IsPatch(request.Method)
            && request.Path.StartsWithSegments("/api/verifications", StringComparison.OrdinalIgnoreCase))
        {
            policy = Verification;
            return true;
        }

        policy = default;
        return false;
    }

    public static string CreateClientKey(HttpContext context)
    {
        var address = Normalize(context.Connection.RemoteIpAddress);
        var source = address?.ToString() ?? "unknown";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    public static string CreateAuthenticationClientKey(string? ipAddress, string? identifier)
    {
        var normalizedAddress = IPAddress.TryParse(ipAddress?.Trim(), out var parsedAddress)
            ? Normalize(parsedAddress)?.ToString() ?? "unknown"
            : "unknown";
        var normalizedIdentifier = (identifier ?? string.Empty)
            .Trim()
            .Normalize(NormalizationForm.FormKC)
            .ToUpperInvariant();
        var source = $"{normalizedAddress}\n{normalizedIdentifier}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    private static IPAddress? Normalize(IPAddress? address) =>
        address?.IsIPv4MappedToIPv6 == true ? address.MapToIPv4() : address;
}
