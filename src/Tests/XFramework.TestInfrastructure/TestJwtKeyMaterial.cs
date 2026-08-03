using System.Security.Cryptography;

namespace XFramework.TestInfrastructure;

public static class TestJwtKeyMaterial
{
    private static readonly Lazy<string> PublicKey = new(CreatePublicKey);

    public static string PublicKeyPath => PublicKey.Value;

    private static string CreatePublicKey()
    {
        var directory = Path.Combine(Path.GetTempPath(), "XFramework.TestJwtKeys");
        var path = Path.Combine(directory, "user-jwt-public-key.pem");
        Directory.CreateDirectory(directory);
        if (File.Exists(path))
            return path;

        using var rsa = RSA.Create(2048);
        File.WriteAllText(path, rsa.ExportSubjectPublicKeyInfoPem());
        return path;
    }
}
