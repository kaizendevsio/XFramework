using FluentValidation;
using IdentityServer.Api.Generated;
using IdentityServer.Api.Services;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Bolt.Hub.Extensions;
using Testcontainers.PostgreSql;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Domain.Contexts;
using XFramework.Extensions;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using Contracts = IdentityServer.Domain.Shared.Contracts;

namespace IdentityServer.IntegrationTests;

[SetUpFixture]
public class IntegrationTestFixture
{
    private static PostgreSqlContainer _postgres = null!;
    private static WebApplication _streamFlowApp = null!;
    private static WebApplication _identityServerApp = null!;
    private static WebApplication _testClientApp = null!;
    private static Task? _streamFlowTask;
    private static Task? _identityServerTask;
    private static Task? _testClientTask;

    public static string ConnectionString { get; private set; } = null!;
    public static string BoltUrl => "http://localhost:17000";
    public static string IdentityServerUrl => "http://localhost:18261";
    public static string TestClientUrl => "http://localhost:18262";

    public static IServiceProvider Services => _identityServerApp.Services;

    /// <summary>
    /// Service wrapper that calls IdentityServer through the actual Bolt transport.
    /// </summary>
    public static IIdentityServerServiceWrapper ServiceWrapper =>
        _testClientApp.Services.GetRequiredService<IIdentityServerServiceWrapper>();

    public static readonly Guid TestTenantId = Guid.Parse("7602c2d3-01df-4bdb-9a67-02c144e4a2ac");

    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        // 1. Start Postgres container
        _postgres = new PostgreSqlBuilder()
            .WithDatabase("XFramework_Test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        // 2. Run migrations and seed data
        await MigrateAndSeed();

        // 3. Start Bolt hub
        _streamFlowApp = StartBolt();
        await WaitForHealth($"{BoltUrl}/health/live", _streamFlowTask);

        // 4. Start IdentityServer (connects to Bolt as "IdentityServer" client)
        _identityServerApp = StartIdentityServer();
        await WaitForHealth($"{IdentityServerUrl}/health/live", _identityServerTask);

        // 5. Seed the in-memory tenant cache
        var cache = _identityServerApp.Services.GetRequiredService<IMemoryCache>();
        cache.Set($"GetTenant-{TestTenantId}", new Contracts.Tenant
        {
            Id = TestTenantId,
            TenantId = TestTenantId,
            Name = "Test Tenant",
            Description = "Integration test tenant"
        });

        // 6. Start test client app (connects to Bolt, has IIdentityServerServiceWrapper)
        _testClientApp = StartTestClient();
        await WaitForHealth($"{TestClientUrl}/health/live", _testClientTask);

        // 7. Wait for both Bolt clients to connect and register
        await WaitForBoltClients();

        // 8. Register IdentityServer's generated Bolt handlers on its SignalR connection.
        //    ScanAndRegisterHandlers() only scans the entry assembly (testhost in tests),
        //    so we manually scan the IdentityServer assembly for ISignalREventHandler implementations.
        RegisterBoltHandlers();
    }

    [OneTimeTearDown]
    public async Task GlobalTeardown()
    {
        try { if (_testClientApp != null) await _testClientApp.StopAsync(); } catch { }
        try { if (_identityServerApp != null) await _identityServerApp.StopAsync(); } catch { }
        try { if (_streamFlowApp != null) await _streamFlowApp.StopAsync(); } catch { }
        if (_postgres != null) await _postgres.DisposeAsync();
    }

    private static WebApplication StartBolt()
    {
        var builder = XApplication.Configure<Bolt.Hub.Installers.BoltInstaller>();
        builder.WebHost.UseUrls(BoltUrl);
        OverrideConfiguration(builder, "Bolt.Test", "00000000-0000-0000-0000-000000000001");

        var app = (WebApplication)builder.Build();
        app.UseCorrelationId();
        app.UseAppServices();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _streamFlowTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static WebApplication StartIdentityServer()
    {
        var builder = XApplication.Configure<AuthService>();
        builder.WebHost.UseUrls(IdentityServerUrl);
        // ClientName = "IdentityServer.Test" → SignalRService registers as SHA256("IdentityServer")
        // This matches the generated wrapper's TargetClient
        OverrideConfiguration(builder, "IdentityServer.Test", "3902761a-822d-4c6b-8e2d-323fd501bcd6");
        builder.Configuration["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/stream-flow/queue";

        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddValidatorsFromAssemblyContaining<AuthService>();

        var app = (WebApplication)builder.Build();
        app.UseCorrelationId();
        app.MapGeneratedEndpoints();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _identityServerTask = Task.Run(() => app.RunAsync());
        return app;
    }

    /// <summary>
    /// Minimal app that acts as a Bolt client with IIdentityServerServiceWrapper.
    /// This is how any real service (Blazor, Wallets, etc.) would call IdentityServer via Bolt.
    /// </summary>
    private static WebApplication StartTestClient()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(TestClientUrl);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BoltConfiguration:ClientName"] = "TestClient",
            ["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/stream-flow/queue",
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Serilog:MinimumLevel:Default"] = "Warning",
        });

        // Core services needed by SignalRService and service wrappers
        builder.Services.InstallStandardServices<IntegrationTestFixture>(builder.Configuration);
        builder.Services.AddSingleton(new XFramework.Domain.Shared.BusinessObjects.DeviceAgentProvider("IntegrationTest"));
        builder.Services.AddSingleton<IMessageBusWrapper, BoltDriverSignalR>();

        // Register the IdentityServer service wrapper (generated — uses Bolt transport)
        builder.Services.AddIdentityServerWrapperServices();

        var app = builder.Build();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _testClientTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static async Task WaitForBoltClients()
    {
        var idServerSignalR = _identityServerApp.Services.GetRequiredService<ISignalRService>();
        var testClientSignalR = _testClientApp.Services.GetRequiredService<ISignalRService>();

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var idConnected = idServerSignalR.Connection?.State == HubConnectionState.Connected;
            var testConnected = testClientSignalR.Connection?.State == HubConnectionState.Connected;
            if (idConnected && testConnected)
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
        var signalRService = _identityServerApp.Services.GetRequiredService<ISignalRService>();
        var logger = _identityServerApp.Services.GetRequiredService<ILogger<BaseSignalRHandler>>();
        var scopeFactory = _identityServerApp.Services.GetRequiredService<IServiceScopeFactory>();

        var handlers = typeof(AuthService).Assembly.GetExportedTypes()
            .Where(t => typeof(ISignalREventHandler).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(Activator.CreateInstance)
            .Cast<ISignalREventHandler>();

        foreach (var handler in handlers)
            handler.Handle(signalRService.Connection!, logger, scopeFactory);
    }

    private static void OverrideConfiguration(WebApplicationBuilder builder, string clientName, string clientGuid)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultDatabaseConnection"] = ConnectionString,
            ["BoltConfiguration:ClientGuid"] = clientGuid,
            ["BoltConfiguration:ClientName"] = clientName,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["JwtOptions:ValidAudience"] = "http://localhost:18261",
            ["JwtOptions:ValidIssuer"] = "http://localhost:18261",
            ["JwtOptions:Secret"] = "Mm1VFHaqZ7MoVJyZd1zrAKxTpsXbYG6RqSMKYG2cV7RBBUdmsm97HOfKyA7MZ1LUl77ZklJPJfnegohyHqJIoQ983fTKmJcY",
            ["JwtOptions:AccessTokenLifespan"] = "00:30:00",
            ["JwtOptions:RefreshTokenLifespan"] = "00:30:00",
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

    private static async Task WaitForHealth(string url, Task? appTask = null, int timeoutSeconds = 30)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            if (appTask is { IsFaulted: true })
            {
                throw new InvalidOperationException(
                    $"Application crashed during startup: {appTask.Exception?.GetBaseException().Message}",
                    appTask.Exception?.GetBaseException());
            }

            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode) return;
            }
            catch { }
            await Task.Delay(500);
        }

        if (appTask is { IsFaulted: true })
        {
            throw new InvalidOperationException(
                $"Application crashed during startup: {appTask.Exception?.GetBaseException().Message}",
                appTask.Exception?.GetBaseException());
        }

        throw new TimeoutException($"Service at {url} did not become healthy within {timeoutSeconds}s");
    }
}

public static class TestData
{
    public static readonly Guid RoleTypeId = XFramework.TestInfrastructure.TestConstants.RoleTypeId;
}
