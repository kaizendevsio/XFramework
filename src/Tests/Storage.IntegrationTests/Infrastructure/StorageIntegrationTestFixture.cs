using System.Runtime.CompilerServices;
using Bolt.Client;
using Bolt.Hub.Extensions;
using FluentValidation;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;
using Storage.Api.Features.Sessions.UploadPart;
using Storage.Api.Generated;
using Storage.Api.Services;
using Storage.Api.Services.Providers;
using Storage.Integration.Drivers;
using Storage.IntegrationTests.Infrastructure;
using Testcontainers.PostgreSql;
using XFramework.Core.DataContext;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Domain.Contexts;
using XFramework.Domain.Interceptors;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.DataContext;
using XFramework.Extensions;
using XFramework.Integration.Extensions;
using XFramework.TestInfrastructure;

namespace Storage.IntegrationTests;

[SetUpFixture]
public sealed class StorageIntegrationTestFixture
{
    private const string ExternalConnectionStringEnvironmentVariable = "STORAGE_TEST_POSTGRES_CONNECTION";
    private const string BoltSignature = "storage-bolt-test-secret";

    private static PostgreSqlContainer postgres = null!;
    private static bool ownsPostgresContainer;
    private static WebApplication boltApp = null!;
    private static WebApplication storageApp = null!;
    private static WebApplication testClientApp = null!;
    private static Task? boltTask;
    private static Task? storageTask;
    private static Task? testClientTask;

    public static string ConnectionString { get; private set; } = null!;
    public static string BoltUrl => TestConstants.Ports.StorageBolt;
    public static string StorageUrl => TestConstants.Ports.StorageServer;
    public static string TestClientUrl => TestConstants.Ports.StorageTestClient;
    public static Guid TestTenantId => TestConstants.TenantId;
    public static IntegrationStorageObjectProvider Provider { get; } = new();

    public static IStorageServiceWrapper ServiceWrapper =>
        testClientApp.Services.GetRequiredService<IStorageServiceWrapper>();

    public static IDataContext DataContext =>
        testClientApp.Services.CreateScope().ServiceProvider.GetRequiredService<IDataContext>();

    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        var externalConnectionString = Environment.GetEnvironmentVariable(ExternalConnectionStringEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(externalConnectionString))
        {
            ConnectionString = externalConnectionString;
        }
        else
        {
            try
            {
                postgres = new PostgreSqlBuilder()
                    .WithDatabase("XFramework_Storage_Test")
                    .WithUsername("test_user")
                    .WithPassword("test_password")
                    .Build();

                await postgres.StartAsync();
                ownsPostgresContainer = true;
                ConnectionString = postgres.GetConnectionString();
            }
            catch (Exception ex) when (
                ex is ArgumentException ||
                ex is TypeInitializationException ||
                ex.Message.Contains("Docker", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore(
                    $"Storage integration tests require a Testcontainers-compatible Docker endpoint or {ExternalConnectionStringEnvironmentVariable}.");
            }
        }

        await MigrateAndSeed();

        boltApp = StartBolt();
        await TestHostWaiter.WaitForHealth($"{BoltUrl}/health/live", boltTask);

        storageApp = StartStorage();
        await TestHostWaiter.WaitForHealth($"{StorageUrl}/health/live", storageTask);

        testClientApp = StartTestClient();
        await TestHostWaiter.WaitForHealth($"{TestClientUrl}/health/live", testClientTask);

        await WaitForBoltClients();
    }

    [OneTimeTearDown]
    public async Task GlobalTeardown()
    {
        try { if (testClientApp is not null) await testClientApp.StopAsync(); } catch { }
        try { if (storageApp is not null) await storageApp.StopAsync(); } catch { }
        try { if (boltApp is not null) await boltApp.StopAsync(); } catch { }

        if (ownsPostgresContainer && postgres is not null)
            await postgres.DisposeAsync();
    }

    public static AppDbContext CreateDbContext()
    {
        ForceModelAssemblies();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenant:DefaultId"] = TestTenantId.ToString()
            })
            .Build();

        return new AppDbContext(options, new HttpContextAccessor(), configuration);
    }

    private static WebApplication StartBolt()
    {
        var builder = XApplication.Configure<Bolt.Hub.Installers.BoltInstaller>();
        builder.WebHost.UseUrls(BoltUrl);
        OverrideConfig(builder, "Bolt.StorageTest", "00000000-0000-0000-0000-000000000690");

        var app = (WebApplication)builder.Build();
        app.UseCorrelationId();
        app.UseAppServices();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        boltTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static WebApplication StartStorage()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(StorageUrl);
        OverrideConfig(builder, "Storage", "2cc8a10f-54f1-44da-99e4-d49e3f663d19");
        builder.Configuration["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/bolt/ws";

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<AuditInterceptor>();
        builder.Services.AddDbContext<DbContext, AppDbContext>((sp, options) => options
            .UseNpgsql(ConnectionString,
                npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .ConfigureWarnings(w => w.Ignore(
                RelationalEventId.BoolWithDefaultWarning,
                RelationalEventId.PendingModelChangesWarning))
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));
        builder.Services.AddServerDataContext<AppDbContext>();
        builder.Services.InstallStandardServices<StorageService>(builder.Configuration);
        builder.Services.AddTenantResolver();
        builder.Services.AddTenantModuleFeatures();
        builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
        builder.Services.AddScoped<StorageService>();
        builder.Services.AddSingleton(Provider);
        builder.Services.AddScoped<IStorageProviderFactory, IntegrationStorageProviderFactory>();
        builder.Services.AddValidatorsFromAssemblyContaining<StorageService>();
        builder.Services.AddAuthentication("StorageTest")
            .AddScheme<AuthenticationSchemeOptions, StorageTestAuthHandler>("StorageTest", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddXFrameworkBoltClient(builder.Configuration, autoConnect: false);
        builder.Services.AddDataContextHandler(typeof(StorageService).Assembly);

        var app = (WebApplication)builder.Build();
        RegisterStorageBoltHandlers(app);
        app.UseCorrelationId();
        app.UseAuthentication();
        app.UseTenantModuleFeatureGate(options =>
        {
            options.RequireFeature(TenantModuleFeatureKeys.Storage, "/api/storage");
            options.RequireFeature(TenantModuleFeatureKeys.Storage, "/api/storage-files");
            options.RequireFeature(TenantModuleFeatureKeys.Storage, "/api/storage-file-types");
        });
        app.UseAuthorization();

        var securedRoutes = app.MapGroup(string.Empty).RequireAuthorization();
        securedRoutes.MapGeneratedEndpoints();
        securedRoutes.MapUploadStorageFilePartRestEndpoint();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        storageTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static WebApplication StartTestClient()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(TestClientUrl);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BoltConfiguration:ClientName"] = "StorageTestClient",
            ["BoltConfiguration:ClientGuid"] = Guid.NewGuid().ToString(),
            ["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/bolt/ws",
            ["BoltConfiguration:Signature"] = BoltSignature,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Logging:LogLevel:Default"] = "Warning"
        });

        builder.Services.InstallStandardServices<StorageIntegrationTestFixture>(builder.Configuration);
        builder.Services.AddSingleton(new DeviceAgentProvider("StorageTest"));
        builder.Services.AddXFrameworkBoltClient(builder.Configuration, autoConnect: false);
        builder.Services.AddStorageWrapperServices();
        builder.Services.AddRemoteDataContext();
        builder.Services.AddScoped(_ => new RequestMetadata
        {
            TenantId = TestTenantId,
            RequestId = Guid.NewGuid()
        });

        var app = builder.Build();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        testClientTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static void RegisterStorageBoltHandlers(WebApplication app)
    {
        var client = app.Services.GetRequiredService<BoltClient>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Storage.GeneratedBoltHandlers");
        var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

        BoltHandlerRegistry.RegisterAll(client, logger, scopeFactory);
    }

    private static async Task WaitForBoltClients()
    {
        var storageClient = storageApp.Services.GetRequiredService<BoltClient>();
        var testClient = testClientApp.Services.GetRequiredService<BoltClient>();

        await ConnectBoltClient(storageClient, "Storage service");
        await ConnectBoltClient(testClient, "Storage test client");
        await Task.Delay(1000);

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (storageClient.IsConnected && testClient.IsConnected)
            {
                await Task.Delay(1000);
                return;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("Storage Bolt clients failed to connect within 15s");
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

    private static void OverrideConfig(WebApplicationBuilder builder, string clientName, string clientGuid)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultDatabaseConnection"] = ConnectionString,
            ["DefaultDatabaseConnection"] = ConnectionString,
            ["BoltConfiguration:ClientGuid"] = clientGuid,
            ["BoltConfiguration:ClientName"] = clientName,
            ["BoltConfiguration:Signature"] = BoltSignature,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Storage:DefaultProvider"] = "S3Compatible",
            ["Storage:ProviderProfileName"] = "integration",
            ["Storage:BucketPrefix"] = "xframework-test",
            ["Storage:DefaultChunkSizeBytes"] = "4",
            ["Storage:EnforceProviderLimits"] = "false",
            ["Storage:S3:Endpoint"] = "http://storage-provider.integration",
            ["Storage:S3:Region"] = "us-east-1",
            ["Storage:S3:PublicBaseUrl"] = "https://public.storage.integration",
            ["Logging:LogLevel:Default"] = "Warning"
        });
    }

    private static async Task MigrateAndSeed()
    {
        ForceModelAssemblies();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        await TestSeedData.SeedAll(db);
        await SeedStorageReferenceData(db);
        await db.SaveChangesAsync();
    }

    private static async Task SeedStorageReferenceData(AppDbContext db)
    {
        if (!await db.Set<StorageFileType>().IgnoreQueryFilters().AnyAsync(item => item.Id == TestConstants.StorageFileTypeId))
        {
            db.Set<StorageFileType>().Add(new StorageFileType
            {
                Id = TestConstants.StorageFileTypeId,
                TenantId = TestTenantId,
                Name = "Integration File",
                SystemReferenceId = Guid.Parse("91c1a022-8351-4531-928e-94f3a7b40913"),
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true,
                ConcurrencyStamp = Guid.NewGuid()
            });
        }

        if (!await db.Set<StorageFileIdentifierGroup>().IgnoreQueryFilters().AnyAsync(item => item.Id == TestConstants.StorageFileIdentifierGroupId))
        {
            db.Set<StorageFileIdentifierGroup>().Add(new StorageFileIdentifierGroup
            {
                Id = TestConstants.StorageFileIdentifierGroupId,
                TenantId = TestTenantId,
                Name = "Integration",
                SystemReferenceId = Guid.Parse("f5a59cf2-1dd3-4c01-bd67-d1b9a3fc5584"),
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true,
                ConcurrencyStamp = Guid.NewGuid()
            });
        }

        if (!await db.Set<StorageFileIdentifier>().IgnoreQueryFilters().AnyAsync(item => item.Id == TestConstants.StorageFileIdentifierId))
        {
            db.Set<StorageFileIdentifier>().Add(new StorageFileIdentifier
            {
                Id = TestConstants.StorageFileIdentifierId,
                TenantId = TestTenantId,
                Name = "Integration Upload",
                GroupId = TestConstants.StorageFileIdentifierGroupId,
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true,
                ConcurrencyStamp = Guid.NewGuid()
            });
        }
    }

    private static void ForceModelAssemblies()
    {
        RuntimeHelpers.RunClassConstructor(typeof(StorageFile).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(TenantModuleFeature).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(Wallets.Domain.Shared.Contracts.WalletType).TypeHandle);
    }
}
