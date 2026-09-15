using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Notifications.Api.Services.Push;

/// <summary>RFC 8292 Voluntary Application Server Identification: an ES256 JWT bound to the push service origin.</summary>
public static class VapidSigner
{
    // Push services reject tokens valid for more than 24 hours; stay well inside that.
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(6);

    /// <summary>Builds the "vapid t=..., k=..." Authorization header value for one push service origin.</summary>
    public static string CreateAuthorizationHeader(WebPushVapidKeys keys, Uri endpoint, DateTimeOffset now) =>
        $"vapid t={CreateToken(keys, endpoint, now)}, k={keys.PublicKey}";

    public static string CreateToken(WebPushVapidKeys keys, Uri endpoint, DateTimeOffset now)
    {
        var header = PushBase64Url.Encode("""{"typ":"JWT","alg":"ES256"}"""u8);
        var claims = PushBase64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            // The audience is the scheme and host only: a path would not match the push service's check.
            ["aud"] = endpoint.GetLeftPart(UriPartial.Authority),
            ["exp"] = now.Add(DefaultLifetime).ToUnixTimeSeconds(),
            ["sub"] = keys.Subject
        }));

        var signingInput = Encoding.ASCII.GetBytes($"{header}.{claims}");
        using var key = WebPushCrypto.ImportSigningKey(keys.PrivateKeyBytes, keys.PublicKeyBytes);
        // JWS needs the fixed-width r||s form, not the DER encoding ECDsa returns by default.
        var signature = key.SignData(
            signingInput,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{header}.{claims}.{PushBase64Url.Encode(signature)}";
    }
}
