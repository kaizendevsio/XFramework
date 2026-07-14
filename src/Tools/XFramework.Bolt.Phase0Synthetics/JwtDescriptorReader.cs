using System.Text.Json;

namespace XFramework.Bolt.Phase0Synthetics;

public sealed record JwtDescriptor(DateTimeOffset ExpiresAtUtc, string ServiceName);

public static class JwtDescriptorReader
{
    private const string TransportIssuer = "XFramework.IdentityServer";
    private const string TransportAudience = "XFramework.Bolt.Hub";
    private const string TransportScope = "bolt.service";

    public static JwtDescriptor Read(SecretToken token)
    {
        try
        {
            var segments = token.Reveal().Split('.');
            if (segments.Length != 3)
                throw new SyntheticConfigurationException("invalid_expiry_token_claims");

            using var headerDocument = JsonDocument.Parse(DecodeBase64Url(segments[0]));
            using var claimsDocument = JsonDocument.Parse(DecodeBase64Url(segments[1]));
            var header = headerDocument.RootElement;
            var claims = claimsDocument.RootElement;
            if (!IsTransportHeader(header) ||
                !TryReadExpiration(claims, out var expiration) ||
                !HasExpectedAudience(claims) ||
                ReadString(claims, "iss") != TransportIssuer ||
                ReadString(claims, "scope") != TransportScope ||
                string.IsNullOrWhiteSpace(ReadString(claims, "client_credential_generation")))
            {
                throw new SyntheticConfigurationException("invalid_expiry_token_claims");
            }

            var clientId = ReadString(claims, "client_id");
            if (string.IsNullOrWhiteSpace(clientId) ||
                ReadString(claims, "service") != clientId ||
                ReadString(claims, "sub") != clientId)
            {
                throw new SyntheticConfigurationException("invalid_expiry_token_claims");
            }

            return new JwtDescriptor(DateTimeOffset.FromUnixTimeSeconds(expiration), clientId);
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

    private static bool IsTransportHeader(JsonElement header) =>
        ReadString(header, "alg") == "RS256" &&
        ReadString(header, "typ") == "bolt+jwt" &&
        !string.IsNullOrWhiteSpace(ReadString(header, "kid"));

    private static bool HasExpectedAudience(JsonElement claims)
    {
        if (!claims.TryGetProperty("aud", out var audience))
            return false;

        return audience.ValueKind == JsonValueKind.String
            ? audience.GetString() == TransportAudience
            : audience.ValueKind == JsonValueKind.Array &&
              audience.EnumerateArray().Any(static value =>
                  value.ValueKind == JsonValueKind.String && value.GetString() == TransportAudience);
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
}
