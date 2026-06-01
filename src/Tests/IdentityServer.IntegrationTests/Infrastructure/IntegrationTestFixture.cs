using Bolt.Client;
using FluentValidation;
using IdentityServer.Api.Features.Verification.Confirm;
using IdentityServer.Api.Generated;
using IdentityServer.Api.Services;
using IdentityServer.Integration.Drivers;
using Messaging.Integration.Drivers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json.Serialization;
using Bolt.Hub.Extensions;
using Testcontainers.PostgreSql;
using XFramework.Core.DataContext;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Extensions;
using XFramework.Integration.Extensions;
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

    /// <summary>
    /// RemoteDataContext that queries IdentityServer through the Bolt transport.
    /// Creates a new scope each call so tests get a fresh context.
    /// </summary>
    public static IDataContext DataContext =>
        _testClientApp.Services.CreateScope().ServiceProvider.GetRequiredService<IDataContext>();

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
        // Build manually (instead of XApplication.Configure<AuthService>()) so that config
        // overrides are applied BEFORE the installers read BoltConfiguration to create BoltClient.
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(IdentityServerUrl);

        // Override configuration first — installers read config at registration time
        OverrideConfiguration(builder, "IdentityServer", Guid.NewGuid().ToString());
        builder.Configuration["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/bolt/ws";

        // Register services that the installers normally provide, except WrapperInstaller
        // (WrapperInstaller calls AddXFrameworkBoltClient which reads ClientGuid as Guid?,
        // but the service ID is a SHA256 hex string — not a valid GUID).
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddDbContext<DbContext, AppDbContext>((sp, options) => options
            .UseNpgsql(ConnectionString,
                npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.BoolWithDefaultWarning,
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));
        builder.Services.AddServerDataContext<AppDbContext>();

        builder.Services.InstallJwt(builder.Configuration);
        builder.Services.InstallStandardServices<AuthService>(builder.Configuration);
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
        builder.Services.AddTenantResolver();
        builder.Services.AddMessagingWrapperServices();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddValidatorsFromAssemblyContaining<AuthService>();
        builder.Services.AddXFrameworkBoltClient(builder.Configuration);

        // Register DataContext handler so IdentityServer can serve __db_query__/__db_changes__ via Bolt
        builder.Services.AddDataContextHandler(typeof(AuthService).Assembly);

        var app = (WebApplication)builder.Build();
        RegisterIdentityServerBoltHandlers(app);
        app.UseCorrelationId();
        app.MapGeneratedEndpoints();
        app.MapConfirmVerificationEndpoint();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _identityServerTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static void RegisterIdentityServerBoltHandlers(WebApplication app)
    {
        var client = app.Services.GetRequiredService<BoltClient>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("IdentityServer.GeneratedBoltHandlers");
        var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

        IdentityServer.Api.Generated.BoltHandlerRegistry.RegisterAll(client, logger, scopeFactory);
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
            ["BoltConfiguration:ClientGuid"] = Guid.NewGuid().ToString(),
            ["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/bolt/ws",
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Logging:LogLevel:Default"] = "Warning",
        });

        // Core services needed by service wrappers.
        // NOTE(Task13): Test client uses thin-protocol BoltDriver. IdentityServer still uses
        // SignalR for handler registration, so StreamFlow tests will time out until Task 13
        // updates the source generator to emit thin-protocol handlers.
        builder.Services.InstallStandardServices<IntegrationTestFixture>(builder.Configuration);
        builder.Services.AddSingleton(new XFramework.Domain.Shared.BusinessObjects.DeviceAgentProvider("IntegrationTest"));
        builder.Services.AddXFrameworkBoltClient(builder.Configuration);

        // Register the IdentityServer service wrapper (generated — uses Bolt transport)
        builder.Services.AddIdentityServerWrapperServices();

        // Register RemoteDataContext so IDataContext queries go through Bolt to IdentityServer
        builder.Services.AddRemoteDataContext();

        // RequestMetadata provides tenant context for remote queries
        builder.Services.AddScoped(_ => new RequestMetadata
        {
            TenantId = TestTenantId,
            RequestId = Guid.NewGuid()
        });

        var app = builder.Build();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _testClientTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static async Task WaitForBoltClients()
    {
        var testClient = _testClientApp.Services.GetRequiredService<BoltClient>();

        // Handler registration is now automatic via BoltHandlerRegistrationHostedService
        // when AddXFrameworkBoltClient() is called in the service's startup.
        // Give the BoltClient time to connect and the hosted service to register handlers.
        await Task.Delay(2000);

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (testClient.IsConnected)
            {
                await Task.Delay(1000);
                return;
            }
            await Task.Delay(250);
        }

        throw new TimeoutException("Bolt test client failed to connect within 15s");
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
            ["Logging:LogLevel:Default"] = "Warning"
        });
    }

    private static async Task MigrateAndSeed()
    {
        // Force-load module Domain.Shared assemblies so AppDbContext.OnModelCreating
        // discovers their IEntityTypeConfiguration<T> registrations via AppDomain scan.
        // The CLR lazy-loads assemblies; accessing a type forces immediate load.
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(Wallets.Domain.Shared.Contracts.WalletType).TypeHandle);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
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
