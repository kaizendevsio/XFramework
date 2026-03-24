using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Bolt.Hub.Extensions;
using Testcontainers.PostgreSql;
using Wallets.Integration.Drivers;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Extensions;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using Contracts = IdentityServer.Domain.Shared.Contracts;

namespace Wallets.IntegrationTests;

[SetUpFixture]
public class WalletsTestFixture
{
    private static PostgreSqlContainer _postgres = null!;
    private static WebApplication _streamFlowApp = null!;
    private static WebApplication _walletsApp = null!;
    private static WebApplication _testClientApp = null!;
    private static Task? _streamFlowTask;
    private static Task? _walletsTask;
    private static Task? _testClientTask;

    public static string ConnectionString { get; private set; } = null!;
    public static string BoltUrl => "http://localhost:17100";
    public static string WalletsUrl => "http://localhost:18361";
    public static string TestClientUrl => "http://localhost:18362";

    public static IServiceProvider Services => _walletsApp.Services;
    public static IWalletsServiceWrapper ServiceWrapper =>
        _testClientApp.Services.GetRequiredService<IWalletsServiceWrapper>();

    public static readonly Guid TestTenantId = Guid.Parse("7602c2d3-01df-4bdb-9a67-02c144e4a2ac");
    public static readonly Guid TestWalletTypeId = Guid.Parse("e1e2e3e4-e5f6-7890-abcd-ef1234567890");

    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        _postgres = new PostgreSqlBuilder()
            .WithDatabase("XFramework_Wallets_Test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        await MigrateAndSeed();

        _streamFlowApp = StartBolt();
        await WaitForHealth($"{BoltUrl}/health/live");

        _walletsApp = StartWallets();
        await WaitForHealth($"{WalletsUrl}/health/live");

        var cache = _walletsApp.Services.GetRequiredService<IMemoryCache>();
        cache.Set($"GetTenant-{TestTenantId}", new Contracts.Tenant
        {
            Id = TestTenantId, TenantId = TestTenantId,
            Name = "Test Tenant", Description = "Wallets integration test tenant"
        });

        _testClientApp = StartTestClient();
        await WaitForHealth($"{TestClientUrl}/health/live");

        await WaitForBoltClients();
        RegisterBoltHandlers();
    }

    [OneTimeTearDown]
    public async Task GlobalTeardown()
    {
        try { if (_testClientApp != null) await _testClientApp.StopAsync(); } catch { }
        try { if (_walletsApp != null) await _walletsApp.StopAsync(); } catch { }
        try { if (_streamFlowApp != null) await _streamFlowApp.StopAsync(); } catch { }
        if (_postgres != null) await _postgres.DisposeAsync();
    }

    private static WebApplication StartBolt()
    {
        var builder = XApplication.Configure<Bolt.Hub.Installers.BoltInstaller>();
        builder.WebHost.UseUrls(BoltUrl);
        OverrideConfig(builder, "Bolt.WalletTest", "00000000-0000-0000-0000-000000000010");

        var app = (WebApplication)builder.Build();
        app.UseCorrelationId();
        app.UseAppServices();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _streamFlowTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static WebApplication StartWallets()
    {
        var builder = XApplication.Configure<Wallets.Api.Services.WalletOperationsService>();
        builder.WebHost.UseUrls(WalletsUrl);
        OverrideConfig(builder, "Wallets.Test", "4902761a-822d-4c6b-8e2d-323fd501bcd6");
        builder.Configuration["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/stream-flow/queue";

        builder.Services.AddValidatorsFromAssemblyContaining<Wallets.Api.Services.IWalletOperationsService>();

        var app = (WebApplication)builder.Build();
        app.UseCorrelationId();

        // Map source-generated endpoints
        Wallets.Api.Generated.GeneratedEndpointRoutes.MapGeneratedEndpoints(app);

        // Manual endpoints
        Wallets.Api.Features.Wallets.Get.GetWalletEndpoint.Map(app);
        Wallets.Api.Features.Wallets.GetByCredential.GetWalletsByCredentialEndpoint.Map(app);

        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _walletsTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static WebApplication StartTestClient()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(TestClientUrl);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BoltConfiguration:ClientName"] = "WalletTestClient",
            ["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/stream-flow/queue",
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Serilog:MinimumLevel:Default"] = "Warning",
        });

        builder.Services.InstallStandardServices<WalletsTestFixture>(builder.Configuration);
        builder.Services.AddSingleton(new DeviceAgentProvider("WalletTest"));
        builder.Services.AddSingleton<IMessageBusWrapper, BoltDriverSignalR>();
        builder.Services.AddWalletsWrapperServices();

        var app = builder.Build();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _testClientTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static async Task WaitForBoltClients()
    {
        var walletsSignalR = _walletsApp.Services.GetRequiredService<ISignalRService>();
        var testClientSignalR = _testClientApp.Services.GetRequiredService<ISignalRService>();

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (walletsSignalR.Connection?.State == HubConnectionState.Connected &&
                testClientSignalR.Connection?.State == HubConnectionState.Connected)
            {
                await Task.Delay(1000);
                return;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException("Bolt clients failed to connect within 15s");
    }

    private static void RegisterBoltHandlers()
    {
        var signalRService = _walletsApp.Services.GetRequiredService<ISignalRService>();
        var logger = _walletsApp.Services.GetRequiredService<ILogger<BaseSignalRHandler>>();
        var scopeFactory = _walletsApp.Services.GetRequiredService<IServiceScopeFactory>();

        var handlers = typeof(Wallets.Api.Services.IWalletOperationsService).Assembly.GetExportedTypes()
            .Where(t => typeof(ISignalREventHandler).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(Activator.CreateInstance)
            .Cast<ISignalREventHandler>();

        foreach (var handler in handlers)
            handler.Handle(signalRService.Connection!, logger, scopeFactory);
    }

    private static void OverrideConfig(WebApplicationBuilder builder, string clientName, string clientGuid)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultDatabaseConnection"] = ConnectionString,
            ["BoltConfiguration:ClientGuid"] = clientGuid,
            ["BoltConfiguration:ClientName"] = clientName,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Serilog:MinimumLevel:Default"] = "Warning"
        });
    }

    private static async Task MigrateAndSeed()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        await XFramework.TestInfrastructure.TestSeedData.SeedAll(db);
    }

    private static async Task WaitForHealth(string url, int timeoutSeconds = 30)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode) return;
            }
            catch { }
            await Task.Delay(500);
        }
        throw new TimeoutException($"Service at {url} did not become healthy within {timeoutSeconds}s");
    }
}
