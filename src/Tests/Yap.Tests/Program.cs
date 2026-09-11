namespace Yap.Tests;

internal static class EntryPoint
{
    public static async Task Main(string[] args)
    {
        if (args is not ["--serve"]) throw new ArgumentException("Use dotnet test, or --serve for the isolated browser fixture.");
        await using var app = UiFixture.Create();
        await app.RunAsync();
    }
}
