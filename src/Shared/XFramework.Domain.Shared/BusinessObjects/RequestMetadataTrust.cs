using System.Security.Cryptography;
using System.Text;

namespace XFramework.Domain.Shared.BusinessObjects;

public static class RequestMetadataTrust
{
    private static readonly TimeSpan DefaultMaxAge = TimeSpan.FromMinutes(10);

    public static void Sign(RequestMetadata metadata, string? secret, DateTime? nowUtc = null)
    {
        if (metadata is null || string.IsNullOrWhiteSpace(secret))
        {
            return;
        }

        metadata.RequestId ??= Guid.NewGuid();
        metadata.TrustedAtUtc = NormalizeUtc(nowUtc ?? DateTime.UtcNow);
        metadata.TrustedSignature = ComputeSignature(metadata, secret);
    }

    public static bool IsValid(RequestMetadata? metadata, string? secret, TimeSpan? maxAge = null, DateTime? nowUtc = null)
    {
        if (metadata?.TenantId is null ||
            metadata.TenantId == Guid.Empty ||
            metadata.TrustedAtUtc is null ||
            string.IsNullOrWhiteSpace(metadata.TrustedSignature) ||
            string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        var timestamp = NormalizeUtc(metadata.TrustedAtUtc.Value);
        var now = NormalizeUtc(nowUtc ?? DateTime.UtcNow);
        var allowedAge = maxAge.GetValueOrDefault(DefaultMaxAge);
        if (timestamp < now.Subtract(allowedAge) || timestamp > now.AddMinutes(1))
        {
            return false;
        }

        var expected = ComputeSignature(metadata, secret);
        var suppliedBytes = Encoding.UTF8.GetBytes(metadata.TrustedSignature.Trim().ToLowerInvariant());
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return suppliedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
    }

    private static string ComputeSignature(RequestMetadata metadata, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(BuildPayload(metadata)))).ToLowerInvariant();
    }

    private static string BuildPayload(RequestMetadata metadata)
    {
        var tenantId = metadata.TenantId?.ToString("D") ?? string.Empty;
        var credentialId = metadata.CredentialId?.ToString("D") ?? string.Empty;
        var sessionId = metadata.SessionId?.ToString("D") ?? string.Empty;
        var requestId = metadata.RequestId?.ToString("D") ?? string.Empty;
        var trustedAt = NormalizeUtc(metadata.TrustedAtUtc ?? DateTime.MinValue).ToString("O");
        var name = metadata.Name?.Trim() ?? string.Empty;
        return string.Join('|', tenantId, credentialId, sessionId, requestId, trustedAt, name);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
