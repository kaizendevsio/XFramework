using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Bolt.Tests;

internal static class TestJwtKeyMaterial
{
    private static readonly Lazy<KeyPair> Keys = new(CreateKeyPair);

    public static string PublicKeyPath => Keys.Value.PublicKeyPath;

    public static RsaSecurityKey CreateSigningKey(string keyId)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(Keys.Value.PrivateKeyPem);
        return new RsaSecurityKey(rsa) { KeyId = keyId };
    }

    private static KeyPair CreateKeyPair()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "XFramework.Bolt.Tests.JwtKeys",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var publicKeyPath = Path.Combine(directory, "public.pem");
        using var rsa = RSA.Create(2048);
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();
        File.WriteAllText(publicKeyPath, rsa.ExportSubjectPublicKeyInfoPem());
        return new KeyPair(privateKeyPem, publicKeyPath);
    }

    private sealed record KeyPair(string PrivateKeyPem, string PublicKeyPath);
}
