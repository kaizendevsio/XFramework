using Bolt.Client;
using Microsoft.Extensions.Caching.Memory;
using Bolt.Hub.Extensions;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Extensions;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Extensions;
using IdentityServer.Domain.Shared.Contracts;

namespace XFramework.TestInfrastructure;

/// <summary>
/// Shared helpers for setting up Bolt hub, service apps, and test clients.
/// Eliminates duplicated 3-app architecture code across test fixtures.
/// </summary>
public static class BoltTestHelper
{
    public static WebApplication StartBoltHub(string url, string connectionString)
    {
        var builder = XApplication.Configure<Bolt.Hub.Installers.BoltInstaller>();
        builder.WebHost.UseUrls(url);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultDatabaseConnection"] = connectionString,
            ["BoltConfiguration:ClientGuid"] = "00000000-0000-0000-0000-000000000001",
            ["BoltConfiguration:ClientName"] = "Bolt.Test",
            ["Tenant:DefaultId"] = TestConstants.TenantId.ToString(),
            ["Logging:LogLevel:Default"] = "Warning"
        });

        var app = (WebApplication)builder.Build();
        app.UseCorrelationId();
        app.UseAppServices();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        Task.Run(() => app.RunAsync());
        return app;
    }

    /// <summary>
    /// Creates a minimal test client app that connects to Bolt and registers service wrappers.
    /// </summary>
    public static WebApplication StartTestClient(
        string url,
        string streamFlowUrl,
        string clientName,
        Action<IServiceCollection> registerWrappers)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(url);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BoltConfiguration:ClientName"] = clientName,
            ["BoltConfiguration:ClientGuid"] = Guid.NewGuid().ToString(),
            ["BoltConfiguration:ServerUrls:0"] = $"{streamFlowUrl}/bolt/ws",
            ["Tenant:DefaultId"] = TestConstants.TenantId.ToString(),
            ["Logging:LogLevel:Default"] = "Warning",
        });

        // NOTE(Task13): Test client uses thin-protocol BoltDriver. Service apps still use
        // SignalR for handler registration, so StreamFlow tests will time out until Task 13
        // updates the source generator to emit thin-protocol handlers.
        builder.Services.InstallStandardServices<TestConstants>(builder.Configuration);
        builder.Services.AddSingleton(new DeviceAgentProvider("IntegrationTest"));
        builder.Services.AddXFrameworkBoltClient(builder.Configuration);
        registerWrappers(builder.Services);

        var app = builder.Build();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        Task.Run(() => app.RunAsync());
        return app;
    }

    /// <summary>
    /// Seeds the in-memory tenant cache for the service app.
    /// </summary>
    public static void SeedTenantCache(WebApplication app)
    {
        var cache = app.Services.GetRequiredService<IMemoryCache>();
        cache.Set($"GetTenant-{TestConstants.TenantId}", new Tenant
        {
            Id = TestConstants.TenantId,
            TenantId = TestConstants.TenantId,
            Name = "Test Tenant",
            Description = "Integration test tenant"
        });
    }

    /// <summary>
    /// Waits for the test client Bolt connection to be established.
    /// Handler registration is now automatic via BoltHandlerRegistrationHostedService
    /// when AddXFrameworkBoltClient() is called in the service's startup.
    /// </summary>
    public static async Task WaitForBoltClients(WebApplication serviceApp, WebApplication testClientApp)
    {
        var testClientBolt = testClientApp.Services.GetRequiredService<BoltClient>();

        // Give both service and test client time to connect and register handlers.
        await Task.Delay(2000);

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (testClientBolt.IsConnected)
            {
                await Task.Delay(1000);
                return;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException("Bolt test client failed to connect within 15s");
    }

    /// <summary>
    /// Waits for a service to become healthy, with crash detection.
    /// </summary>
    public static async Task WaitForHealth(string url, Task? appTask = null, int timeoutSeconds = 30)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            if (appTask is { IsFaulted: true })
                throw new InvalidOperationException(
                    $"App crashed: {appTask.Exception?.GetBaseException().Message}",
                    appTask.Exception?.GetBaseException());
            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode) return;
            }
            catch { }
            await Task.Delay(500);
        }

        if (appTask is { IsFaulted: true })
            throw new InvalidOperationException(
                $"App crashed: {appTask.Exception?.GetBaseException().Message}",
                appTask.Exception?.GetBaseException());

        throw new TimeoutException($"Service at {url} did not become healthy within {timeoutSeconds}s");
    }

    /// <summary>
    /// Standard configuration overrides for service apps in tests.
    /// </summary>
    public static void OverrideConfiguration(
        WebApplicationBuilder builder,
        string connectionString,
        string clientName,
        string clientGuid,
        string streamFlowUrl)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultDatabaseConnection"] = connectionString,
            ["BoltConfiguration:ClientGuid"] = clientGuid,
            ["BoltConfiguration:ClientName"] = clientName,
            ["BoltConfiguration:ServerUrls:0"] = $"{streamFlowUrl}/stream-flow/queue",
            ["Tenant:DefaultId"] = TestConstants.TenantId.ToString(),
            ["JwtOptions:ValidAudience"] = "http://localhost",
            ["JwtOptions:ValidIssuer"] = "http://localhost",
            ["JwtOptions:Secret"] = "Mm1VFHaqZ7MoVJyZd1zrAKxTpsXbYG6RqSMKYG2cV7RBBUdmsm97HOfKyA7MZ1LUl77ZklJPJfnegohyHqJIoQ983fTKmJcY",
            ["JwtOptions:AccessTokenLifespan"] = "00:30:00",
            ["JwtOptions:RefreshTokenLifespan"] = "00:30:00",
            ["Logging:LogLevel:Default"] = "Warning"
        });
    }
}
