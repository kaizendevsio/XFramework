using Bolt.Client;
using Bolt.Hub.Extensions;
using FluentValidation;
using Inventario.Api.Features.Products.Update;
using Inventario.Api.Infrastructure;
using Inventario.Integration.Drivers;
using Communications.Integration.Drivers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using XFramework.Core.DataContext;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Domain.Contexts;
using XFramework.Domain.Interceptors;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Extensions;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Extensions;
using XFramework.Integration.Extensions;
using XFramework.Integration.Security;
using XFramework.Inventario.Api.Services;
using XFramework.TestInfrastructure;

namespace Inventario.IntegrationTests;

[SetUpFixture]
public sealed class InventarioIntegrationTestFixture
{
    private static PostgreSqlContainer _postgres = null!;
    private static WebApplication _boltApp = null!;
    private static WebApplication _inventarioApp = null!;
    private static WebApplication _testClientApp = null!;
    private static IServiceScope _testClientScope = null!;
    private static Task? _boltTask;
    private static Task? _inventarioTask;
    private static Task? _testClientTask;
    private static readonly TestBoltTransportAuthority TransportAuthority = new(BoltUrl);
    private static readonly TestInvocationIdentityOptions InvocationIdentity = new(
        "inventario-test-actor-token",
        "inventario-test-service-token",
        XFrameworkServiceNames.Portal,
        TestConstants.TenantId,
        Guid.Parse("00000000-0000-0000-0000-000000000821"),
        Guid.Parse("00000000-0000-0000-0000-000000000822"),
        Guid.Parse("00000000-0000-0000-0000-000000000823"));

    public static string ConnectionString { get; private set; } = null!;
    public static string BoltUrl => TestConstants.Ports.InventarioBolt;
    public static string InventarioUrl => TestConstants.Ports.InventarioServer;
    public static string TestClientUrl => TestConstants.Ports.InventarioTestClient;
    public static Guid TestTenantId => TestConstants.TenantId;
    public static string TestActorAccessToken => InvocationIdentity.ActorToken;

    public static IServiceProvider Services => _inventarioApp.Services;
    public static IInventarioServiceWrapper ServiceWrapper =>
        _testClientScope.ServiceProvider.GetRequiredService<IInventarioServiceWrapper>();
    public static IDataContext DataContext =>
        _testClientApp.Services.CreateScope().ServiceProvider.GetRequiredService<IDataContext>();

    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        _postgres = new PostgreSqlBuilder()
            .WithDatabase("XFramework_Inventario_Test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        await MigrateAndSeed();

        _boltApp = StartBolt();
        await TestHostWaiter.WaitForHealth($"{BoltUrl}/health/live", _boltTask);

        _inventarioApp = StartInventario();
        await TestHostWaiter.WaitForHealth($"{InventarioUrl}/health/live", _inventarioTask);

        _testClientApp = StartTestClient();
        await TestHostWaiter.WaitForHealth($"{TestClientUrl}/health/live", _testClientTask);
        _testClientScope = _testClientApp.Services.CreateScope();

        await WaitForBoltClients();
    }

    [OneTimeTearDown]
    public async Task GlobalTeardown()
    {
        try { _testClientScope?.Dispose(); } catch { }
        try { if (_testClientApp != null) await _testClientApp.StopAsync(); } catch { }
        try { if (_inventarioApp != null) await _inventarioApp.StopAsync(); } catch { }
        try { if (_boltApp != null) await _boltApp.StopAsync(); } catch { }
        if (_postgres != null) await _postgres.DisposeAsync();
        TransportAuthority.Dispose();
    }

    private static WebApplication StartBolt()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseUrls(BoltUrl);
        OverrideConfig(builder, "Bolt.InventarioTest", BoltUrl);
        TransportAuthority.Configure(builder);
        builder.Services.InstallServicesInAssembly<Bolt.Hub.Installers.BoltInstaller>(
            builder.Configuration,
            builder.Environment);
        builder.Services.InstallSwagger(builder.Configuration);
        builder.Services.InstallOData(builder.Configuration);
        builder.Services.InstallJwt(builder.Configuration);
        builder.Services.InstallStandardServices<Bolt.Hub.Installers.BoltInstaller>(builder.Configuration);
        builder.Services.InstallRuntimeServices(builder.Configuration);
        builder.Services.AddTestInvocationClient(InvocationIdentity);

        var app = (WebApplication)builder.Build();
        TransportAuthority.MapEndpoints(app);
        app.UseCorrelationId();
        app.UseAppServices();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        StartApplication(app);
        _boltTask = app.WaitForShutdownAsync();
        return app;
    }

    private static WebApplication StartInventario()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(InventarioUrl);
        OverrideConfig(builder, XFrameworkServiceNames.Inventario, InventarioUrl);
        builder.Configuration["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/bolt/ws";

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<AuditInterceptor>();
        builder.Services.AddDbContext<DbContext, AppDbContext>((sp, options) => options
            .UseNpgsql(ConnectionString,
                npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.BoolWithDefaultWarning,
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));
        builder.Services.AddServerDataContext<AppDbContext>();
        builder.Services.InstallStandardServices<ProductService>(builder.Configuration);
        builder.Services.AddMemoryCaching();
        builder.Services.AddSingleton<IDistributedCache>(_ => null!);
        builder.Services.AddSingleton<IConnectionMultiplexer>(_ => null!);
        builder.Services.AddCommunicationsWrapperServices();
        builder.Services.AddTenantResolver();
        builder.Services.AddTenantModuleFeatures();
        builder.Services.AddScoped<ProductService>();
        builder.Services.AddScoped<StockPostingService>();
        builder.Services.AddScoped<WarehouseService>();
        builder.Services.AddScoped<ReservationService>();
        builder.Services.AddScoped<InventoryAllocationService>();
        builder.Services.AddScoped<InventoryLotService>();
        builder.Services.AddScoped<InventoryPlanningService>();
        builder.Services.AddScoped<InventoryReportingService>();
        builder.Services.AddScoped<PurchasingService>();
        builder.Services.AddScoped<ProductVariationService>();
        builder.Services.AddValidatorsFromAssemblyContaining<ProductService>();
        builder.Services.AddAuthentication("InventarioTest")
            .AddScheme<AuthenticationSchemeOptions, InventarioTestAuthHandler>("InventarioTest", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddXFrameworkBoltClient(builder.Configuration, autoConnect: false);
        builder.Services.AddSingleton(
            TransportAuthority.CreateTokenProvider(XFrameworkServiceNames.Inventario));
        builder.Services.AddTestInvocationServer(InvocationIdentity);
        builder.Services.AddDataContextHandler(typeof(ProductService).Assembly);

        var app = (WebApplication)builder.Build();
        RegisterInventarioBoltHandlers(app);
        app.UseCorrelationId();
        app.UseAuthentication();
        app.UseTenantModuleFeatureGate(InventarioFeatureGateRoutes.Configure);
        app.UseAuthorization();

        var authorizedRoutes = app.MapGroup("").RequireAuthorization();
        Inventario.Api.Generated.GeneratedEndpointRoutes.MapGeneratedEndpoints(authorizedRoutes);
        UpdateProductEndpoint.Map(authorizedRoutes);
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        StartApplication(app);
        _inventarioTask = app.WaitForShutdownAsync();
        return app;
    }

    private static void RegisterInventarioBoltHandlers(WebApplication app)
    {
        var client = app.Services.GetRequiredService<BoltClient>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Inventario.GeneratedBoltHandlers");
        var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

        Inventario.Api.Generated.BoltHandlerRegistry.RegisterAll(client, logger, scopeFactory);
    }

    private static WebApplication StartTestClient()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(TestClientUrl);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BoltConfiguration:ClientName"] = XFrameworkServiceNames.Portal,
            ["BoltConfiguration:ClientGuid"] = Guid.NewGuid().ToString(),
            ["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/bolt/ws",
            ["ServiceIdentity:ClientId"] = XFrameworkServiceNames.Portal,
            ["ServiceIdentity:Authority"] = BoltUrl,
            ["ServiceIdentity:AllowInsecureHttp"] = "true",
            ["ServiceIdentity:GenerationId"] = "inventario-test-client-g1",
            ["ServiceIdentity:ClientSecret"] = "inventario-test-client-secret-2026-secure",
            ["ServiceIdentity:DefaultScopes:0"] = XFrameworkServiceScopes.BoltService,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Kestrel:Endpoints:Http:Url"] = TestClientUrl,
            ["urls"] = TestClientUrl,
            ["Logging:LogLevel:Default"] = "Warning"
        });

        builder.Services.InstallStandardServices<InventarioIntegrationTestFixture>(builder.Configuration);
        builder.Services.AddSingleton(new DeviceAgentProvider("InventarioTest"));
        builder.Services.AddXFrameworkBoltClient(builder.Configuration, autoConnect: false);
        builder.Services.AddSingleton(
            TransportAuthority.CreateTokenProvider(XFrameworkServiceNames.Portal));
        builder.Services.AddTestInvocationClient(InvocationIdentity);
        builder.Services.AddInventarioWrapperServices();
        builder.Services.AddRemoteDataContext();
        builder.Services.AddScoped(_ => new RequestMetadata
        {
            RequestedTenantId = TestTenantId,
            RequestId = Guid.NewGuid()
        });

        var app = builder.Build();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        StartApplication(app);
        _testClientTask = app.WaitForShutdownAsync();
        return app;
    }

    private static void StartApplication(WebApplication app)
    {
        app.StartAsync()
            .WaitAsync(TimeSpan.FromSeconds(30))
            .GetAwaiter()
            .GetResult();
    }

    private static async Task WaitForBoltClients()
    {
        var inventarioClient = _inventarioApp.Services.GetRequiredService<BoltClient>();
        var testClient = _testClientApp.Services.GetRequiredService<BoltClient>();

        await ConnectBoltClient(inventarioClient, "Inventario service");
        await ConnectBoltClient(testClient, "Inventario test client");
        await Task.Delay(1000);

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (inventarioClient.IsConnected && testClient.IsConnected)
            {
                await Task.Delay(1000);
                return;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("Inventario Bolt clients failed to connect within 15s");
    }

    private static async Task ConnectBoltClient(BoltClient client, string clientName)
    {
        if (client.IsConnected)
            return;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await client.ConnectWithRetryAsync(cts.Token);
        }
        catch (OperationCanceledException ex)
        {
            throw new TimeoutException($"{clientName} Bolt client failed to connect within 15s", ex);
        }
    }

    private static void OverrideConfig(
        WebApplicationBuilder builder,
        string clientName,
        string serverUrl)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultDatabaseConnection"] = ConnectionString,
            ["BoltConfiguration:ClientGuid"] = Guid.NewGuid().ToString(),
            ["BoltConfiguration:ClientName"] = clientName,
            ["ServiceIdentity:ClientId"] = clientName,
            ["ServiceIdentity:Authority"] = BoltUrl,
            ["ServiceIdentity:AllowInsecureHttp"] = "true",
            ["ServiceIdentity:GenerationId"] = "inventario-test-service-g1",
            ["ServiceIdentity:ClientSecret"] = "inventario-test-service-secret-2026-secure",
            ["ServiceIdentity:DefaultScopes:0"] = XFrameworkServiceScopes.BoltService,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Kestrel:Endpoints:Http:Url"] = serverUrl,
            ["urls"] = serverUrl,
            ["Logging:LogLevel:Default"] = "Warning"
        });
    }

    private static async Task MigrateAndSeed()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(IdentityServer.Domain.Shared.Contracts.IdentityCredential).TypeHandle);
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(Wallets.Domain.Shared.Contracts.WalletType).TypeHandle);
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(XFramework.Inventario.Domain.Shared.Contracts.Product).TypeHandle);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var db = new AppDbContext(
            options,
            new HttpContextAccessor(),
            new ConfigurationBuilder().Build(),
            new TestEffectiveTenantContextAccessor(TestTenantId));
        await db.Database.MigrateAsync();
        await TestSeedData.SeedAll(db);
        await SeedInvocationActor(db);
        await EnableInventarioFeatures(db);
    }

    private static async Task SeedInvocationActor(AppDbContext db)
    {
        if (await db.Set<IdentityServer.Domain.Shared.Contracts.IdentityCredential>()
                .IgnoreQueryFilters()
                .AnyAsync(x => x.Id == InvocationIdentity.CredentialId))
        {
            return;
        }

        db.Set<IdentityServer.Domain.Shared.Contracts.IdentityInformation>().Add(new()
        {
            Id = InvocationIdentity.IdentityId,
            TenantId = InvocationIdentity.TenantId,
            FirstName = "Inventario",
            LastName = "Integration Actor",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        });

        db.Set<IdentityServer.Domain.Shared.Contracts.IdentityCredential>().Add(new()
        {
            Id = InvocationIdentity.CredentialId,
            TenantId = InvocationIdentity.TenantId,
            IdentityInfoId = InvocationIdentity.IdentityId,
            UserName = "inventario-integration-actor",
            PasswordByte = [0],
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        });

        db.Set<IdentityServer.Domain.Shared.Contracts.IdentityRole>().Add(new()
        {
            Id = Guid.NewGuid(),
            TenantId = InvocationIdentity.TenantId,
            CredentialId = InvocationIdentity.CredentialId,
            TypeId = TestConstants.RoleTypeId,
            RoleExpiration = DateTime.UtcNow.AddYears(1),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        });

        await db.SaveChangesAsync();
    }

    private static async Task EnableInventarioFeatures(AppDbContext db)
    {
        await TestInventarioSeed.SetInventarioFeature(db, string.Empty, true);
        await TestInventarioSeed.SetInventarioFeature(db, IdentityServer.Domain.Shared.Contracts.TenantModuleFeatureKeys.CatalogSubFeature, true);
        await TestInventarioSeed.SetInventarioFeature(db, IdentityServer.Domain.Shared.Contracts.TenantModuleFeatureKeys.WarehousingSubFeature, true);
        await TestInventarioSeed.SetInventarioFeature(db, IdentityServer.Domain.Shared.Contracts.TenantModuleFeatureKeys.StockBalancesSubFeature, true);
        await TestInventarioSeed.SetInventarioFeature(db, IdentityServer.Domain.Shared.Contracts.TenantModuleFeatureKeys.MovementsSubFeature, true);
        await TestInventarioSeed.SetInventarioFeature(db, IdentityServer.Domain.Shared.Contracts.TenantModuleFeatureKeys.ReservationsSubFeature, true);
        await TestInventarioSeed.SetInventarioFeature(db, IdentityServer.Domain.Shared.Contracts.TenantModuleFeatureKeys.TraceabilitySubFeature, true);
        await TestInventarioSeed.SetInventarioFeature(db, IdentityServer.Domain.Shared.Contracts.TenantModuleFeatureKeys.PlanningSubFeature, true);
        await TestInventarioSeed.SetInventarioFeature(db, IdentityServer.Domain.Shared.Contracts.TenantModuleFeatureKeys.InventarioReportingSubFeature, true);
        await TestInventarioSeed.SetInventarioFeature(db, IdentityServer.Domain.Shared.Contracts.TenantModuleFeatureKeys.PurchasingSubFeature, true);
        await TestInventarioSeed.SetInventarioFeature(db, IdentityServer.Domain.Shared.Contracts.TenantModuleFeatureKeys.VariationsSubFeature, true);
    }
}
