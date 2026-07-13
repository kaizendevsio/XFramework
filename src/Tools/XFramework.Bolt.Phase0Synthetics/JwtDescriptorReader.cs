using System.Text.Json;

namespace XFramework.Bolt.Phase0Synthetics;

public sealed record JwtDescriptor(DateTimeOffset ExpiresAtUtc, string? ServiceName);

public static class JwtDescriptorReader
{
    public static JwtDescriptor Read(SecretToken token)
    {
        try
        {
            var segments = token.Reveal().Split('.');
            if (segments.Length != 3)
                throw new SyntheticConfigurationException("invalid_expiry_token_claims");

            var payload = DecodeBase64Url(segments[1]);
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (!TryReadExpiration(root, out var expiration))
                throw new SyntheticConfigurationException("invalid_expiry_token_claims");

            var serviceName = HasServiceScope(root) ? ReadServiceName(root) : null;
            if (HasServiceScope(root) && string.IsNullOrWhiteSpace(serviceName))
                throw new SyntheticConfigurationException("invalid_expiry_token_claims");

            return new JwtDescriptor(DateTimeOffset.FromUnixTimeSeconds(expiration), serviceName);
        }
        catch (SyntheticConfigurationException)
        {
            throw;
        }
        catch
        {
            throw new SyntheticConfigurationException("invalid_expiry_token_claims");
        }
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty
        };
        return Convert.FromBase64String(padded);
    }

    private static bool TryReadExpiration(JsonElement root, out long expiration)
    {
        expiration = default;
        if (!root.TryGetProperty("exp", out var element))
            return false;

        return element.ValueKind == JsonValueKind.Number
            ? element.TryGetInt64(out expiration)
            : element.ValueKind == JsonValueKind.String && long.TryParse(element.GetString(), out expiration);
    }

    private static bool HasServiceScope(JsonElement root)
    {
        foreach (var claimName in new[] { "scope", "scp" })
        {
            if (!root.TryGetProperty(claimName, out var element))
                continue;

            if (element.ValueKind == JsonValueKind.String &&
                element.GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains("bolt.service", StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            if (element.ValueKind == JsonValueKind.Array &&
                element.EnumerateArray().Any(static value =>
                    value.ValueKind == JsonValueKind.String &&
                    string.Equals(value.GetString(), "bolt.service", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadServiceName(JsonElement root)
    {
        foreach (var claimName in new[] { "client_id", "service", "azp", "sub" })
        {
            if (root.TryGetProperty(claimName, out var element) && element.ValueKind == JsonValueKind.String)
                return element.GetString();
        }

        return null;
    }
}
