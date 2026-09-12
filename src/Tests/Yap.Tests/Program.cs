namespace Yap.Tests;

internal static class EntryPoint
{
    public static async Task Main(string[] args)
    {
        if (args is not ["--serve"]) throw new ArgumentException("Use dotnet test, or --serve for the isolated browser fixture.");
        await using var app = UiFixture.Create(int.TryParse(Environment.GetEnvironmentVariable("YAP_FIXTURE_PORT"), out var port) ? port : 5189);
        await app.RunAsync();
    }
}
