namespace IdentityServer.Benchmarks;

internal static class BenchmarkJwtKeyMaterial
{
    private static readonly string DirectoryPath = Path.Combine(
        Path.GetTempPath(),
        "XFramework.IdentityServer.Benchmarks.JwtKeys");

    public static string PrivateKeyPath => Path.Combine(DirectoryPath, "private.pem");
    public static string PublicKeyPath => Path.Combine(DirectoryPath, "public.pem");
}
