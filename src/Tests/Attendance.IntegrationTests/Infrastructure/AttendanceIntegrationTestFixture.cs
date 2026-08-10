using System.Runtime.CompilerServices;
using Attendance.Api.Services;
using Attendance.Domain.Shared.Contracts;
using Attendance.IntegrationTests.Infrastructure;
using Attendance.Integration.Drivers;
using Bolt.Client;
using Bolt.Server;
using FluentValidation;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using XFramework.Core.DataContext;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Contexts;
using XFramework.Domain.Interceptors;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Extensions;
using XFramework.Integration.Extensions;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;

namespace Attendance.IntegrationTests;

[SetUpFixture]
public sealed class AttendanceIntegrationTestFixture
{
    private const string ExternalConnectionStringEnvironmentVariable = "ATTENDANCE_TEST_POSTGRES_CONNECTION";

    private static PostgreSqlContainer postgres = null!;
    private static bool ownsPostgresContainer;
    private static WebApplication boltApp = null!;
    private static WebApplication attendanceApp = null!;
    private static WebApplication testClientApp = null!;
    private static IServiceScope testClientScope = null!;
    private static Task? boltTask;
    private static Task? attendanceTask;
    private static Task? testClientTask;
    private static string? previousAspNetCoreEnvironment;
    private static string? previousJwtPublicKeyPath;
    private static string? previousBoltAnonymous;

    public static string ConnectionString { get; private set; } = null!;
    public static string BoltUrl => TestConstants.Ports.AttendanceBolt;
    public static string AttendanceUrl => TestConstants.Ports.AttendanceServer;
    public static string TestClientUrl => TestConstants.Ports.AttendanceTestClient;
    public static Guid TestTenantId => TestConstants.TenantId;
    public static Guid OtherTenantId { get; } = Guid.Parse("35df2f5d-0c90-4709-9909-0fd0df6869b1");

    public static IServiceProvider Services => attendanceApp.Services;
    public static IAttendanceServiceWrapper ServiceWrapper =>
        testClientScope.ServiceProvider.GetRequiredService<IAttendanceServiceWrapper>();
    public static IDataContext DataContext =>
        testClientApp.Services.CreateScope().ServiceProvider.GetRequiredService<IDataContext>();

    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        previousAspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        previousJwtPublicKeyPath = Environment.GetEnvironmentVariable("JwtOptions__SigningPublicKeyPath");
        previousBoltAnonymous = Environment.GetEnvironmentVariable("BoltConfiguration__Anonymous");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Development);
        Environment.SetEnvironmentVariable("JwtOptions__SigningPublicKeyPath", TestJwtKeyMaterial.PublicKeyPath);
        Environment.SetEnvironmentVariable("BoltConfiguration__Anonymous", "true");

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
                    .WithDatabase("XFramework_Attendance_Test")
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
                if (string.Equals(
                        Environment.GetEnvironmentVariable("CI"),
                        "true",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw;
                }

                Assert.Ignore(
                    $"Attendance integration tests require a Testcontainers-compatible Docker endpoint or {ExternalConnectionStringEnvironmentVariable}.");
            }
        }

        await MigrateAndSeed();

        boltApp = StartBolt();
        await TestHostWaiter.WaitForHealth($"{BoltUrl}/health/live", boltTask);

        attendanceApp = StartAttendance();
        await TestHostWaiter.WaitForHealth($"{AttendanceUrl}/health/live", attendanceTask);

        testClientApp = StartTestClient();
        await TestHostWaiter.WaitForHealth($"{TestClientUrl}/health/live", testClientTask);
        testClientScope = testClientApp.Services.CreateScope();

        await WaitForBoltClients();
    }

    [OneTimeTearDown]
    public async Task GlobalTeardown()
    {
        try { testClientScope?.Dispose(); } catch { }
        try { if (testClientApp is not null) await testClientApp.StopAsync(); } catch { }
        try { if (attendanceApp is not null) await attendanceApp.StopAsync(); } catch { }
        try { if (boltApp is not null) await boltApp.StopAsync(); } catch { }

        if (ownsPostgresContainer && postgres is not null)
            await postgres.DisposeAsync();

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousAspNetCoreEnvironment);
        Environment.SetEnvironmentVariable("JwtOptions__SigningPublicKeyPath", previousJwtPublicKeyPath);
        Environment.SetEnvironmentVariable("BoltConfiguration__Anonymous", previousBoltAnonymous);
    }

    public static AppDbContext CreateDbContext()
    {
        ForceModelAssemblies();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenant:DefaultId"] = TestTenantId.ToString()
            })
            .Build();

        return new AppDbContext(
            options,
            new Microsoft.AspNetCore.Http.HttpContextAccessor(),
            configuration,
            new XFramework.TestInfrastructure.TestEffectiveTenantContextAccessor(TestTenantId));
    }

    private static WebApplication StartBolt()
    {
        var builder = XApplication.Configure<Bolt.Hub.Installers.BoltInstaller>();
        builder.WebHost.UseUrls(BoltUrl);
        OverrideConfig(builder, "Bolt.AttendanceTest", "00000000-0000-0000-0000-000000000030");
        builder.Services.AddSingleton<IServiceTokenProvider, AttendanceTestServiceTokenProvider>();
        builder.Services.AddSingleton<IServiceIdentityProvider, AttendanceBoltHubTestServiceIdentityProvider>();

        var app = (WebApplication)builder.Build();
        app.UseCorrelationId();
        app.UseRouting();
        app.UseWebSockets();
        app.MapBolt("/bolt/ws");
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        boltTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static WebApplication StartAttendance()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(AttendanceUrl);
        OverrideConfig(builder, "XFramework.Attendance", "1e6e94fb-c4ce-4f2b-98b7-88a40b1d57cf");
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
        builder.Services.InstallStandardServices<AttendanceService>(builder.Configuration);
        builder.Services.AddSingleton(CreateTestJwtOptions());
        builder.Services.AddTenantResolver();
        builder.Services.AddTenantModuleFeatures();
        builder.Services.AddScoped<ITenantCredentialCapabilityService, AttendanceTestCapabilityService>();
        builder.Services.AddScoped<IAttendanceCredentialResolver, AttendanceIntegrationCredentialResolver>();
        builder.Services.AddScoped<AttendanceService>();
        builder.Services.AddScoped<IAttendanceReadService, AttendanceReadService>();
        builder.Services.AddValidatorsFromAssemblyContaining<AttendanceService>();
        builder.Services.AddAuthentication("AttendanceTest")
            .AddScheme<AuthenticationSchemeOptions, AttendanceTestAuthHandler>("AttendanceTest", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddXFrameworkBoltClient(
            builder.Configuration,
            autoConnect: false,
            hostEnvironment: builder.Environment);
        builder.Services.AddSingleton<IOptions<ServiceIdentityOptions>>(
            Options.Create(new ServiceIdentityOptions
            {
                ClientId = "XFramework.Attendance",
                DefaultScopes = [XFrameworkServiceScopes.BoltService]
            }));
        builder.Services.AddScoped<IActorIdentityProvider, AttendanceTestActorIdentityProvider>();
        builder.Services.AddSingleton<IServiceIdentityProvider, AttendanceTestServiceIdentityProvider>();
        builder.Services.AddDataContextHandler(typeof(AttendanceService).Assembly);

        var app = (WebApplication)builder.Build();
        RegisterAttendanceBoltHandlers(app);
        app.UseCorrelationId();
        app.UseAuthentication();
        app.UseTenantModuleFeatureGate(options =>
            options.RequireFeature(TenantModuleFeatureKeys.Attendance, "/api/attendance"));
        app.UseAuthorization();

        var authorizedRoutes = app.MapGroup(string.Empty).RequireAuthorization();
        Attendance.Api.Generated.GeneratedEndpointRoutes.MapGeneratedEndpoints(authorizedRoutes);
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        attendanceTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static void RegisterAttendanceBoltHandlers(WebApplication app)
    {
        var client = app.Services.GetRequiredService<BoltClient>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Attendance.GeneratedBoltHandlers");
        var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

        Attendance.Api.Generated.BoltHandlerRegistry.RegisterAll(client, logger, scopeFactory);
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
            ["BoltConfiguration:Anonymous"] = "true",
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Logging:LogLevel:Default"] = "Warning"
        });

        builder.Services.InstallStandardServices<AttendanceIntegrationTestFixture>(builder.Configuration);
        builder.Services.AddSingleton(CreateTestJwtOptions());
        builder.Services.AddSingleton(new DeviceAgentProvider("AttendanceTest"));
        builder.Services.AddXFrameworkBoltClient(
            builder.Configuration,
            autoConnect: false,
            hostEnvironment: builder.Environment);
        builder.Services.AddSingleton<IActorAccessTokenProvider, AttendanceTestActorAccessTokenProvider>();
        builder.Services.AddSingleton<IServiceTokenProvider, AttendanceTestServiceTokenProvider>();
        builder.Services.AddAttendanceWrapperServices();
        builder.Services.AddRemoteDataContext();
        builder.Services.AddScoped(_ => new RequestMetadata
        {
            RequestedTenantId = TestTenantId,
            RequestId = Guid.NewGuid()
        });

        var app = builder.Build();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        testClientTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static async Task WaitForBoltClients()
    {
        var attendanceClient = attendanceApp.Services.GetRequiredService<BoltClient>();
        var testClient = testClientApp.Services.GetRequiredService<BoltClient>();

        await ConnectBoltClient(attendanceClient, "Attendance service");
        await ConnectBoltClient(testClient, "Attendance test client");
        await Task.Delay(1000);

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (attendanceClient.IsConnected && testClient.IsConnected)
            {
                await Task.Delay(1000);
                return;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("Attendance Bolt clients failed to connect within 15s");
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
            ["BoltConfiguration:ClientGuid"] = clientGuid,
            ["BoltConfiguration:ClientName"] = clientName,
            ["BoltConfiguration:Anonymous"] = "true",
            ["ServiceIdentity:ClientId"] = clientName,
            ["ServiceIdentity:Authority"] = BoltUrl,
            ["ServiceIdentity:AllowInsecureHttp"] = "true",
            ["ServiceIdentity:GenerationId"] = "attendance-tests-g1",
            ["ServiceIdentity:ClientSecret"] = "attendance-tests-client-secret-2026",
            ["ServiceIdentity:DefaultScopes:0"] = XFrameworkServiceScopes.BoltService,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Logging:LogLevel:Default"] = "Warning"
        });
    }

    private static JwtOptions CreateTestJwtOptions() => new()
    {
        GenerationId = "attendance-integration-tests-g1",
        SigningPublicKeyPath = TestJwtKeyMaterial.PublicKeyPath,
        ValidAudience = "http://localhost",
        ValidIssuer = "http://localhost",
        AccessTokenLifespan = "00:30:00",
        RefreshTokenLifespan = "00:30:00"
    };

    private static async Task MigrateAndSeed()
    {
        ForceModelAssemblies();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var primaryTenantDb = new AppDbContext(
            options,
            new Microsoft.AspNetCore.Http.HttpContextAccessor(),
            new ConfigurationBuilder().Build(),
            new XFramework.TestInfrastructure.TestEffectiveTenantContextAccessor(TestTenantId));
        await primaryTenantDb.Database.MigrateAsync();
        await TestSeedData.SeedAll(primaryTenantDb);
        await SeedAttendanceFeature(primaryTenantDb, TestTenantId);
        await primaryTenantDb.SaveChangesAsync();

        await using var otherTenantDb = new AppDbContext(
            options,
            new Microsoft.AspNetCore.Http.HttpContextAccessor(),
            new ConfigurationBuilder().Build(),
            new XFramework.TestInfrastructure.TestEffectiveTenantContextAccessor(OtherTenantId));
        await SeedTenant(otherTenantDb, OtherTenantId, "Other Attendance Tenant");
        await SeedAttendanceFeature(otherTenantDb, OtherTenantId);
        await otherTenantDb.SaveChangesAsync();
    }

    private static async Task SeedTenant(AppDbContext db, Guid tenantId, string name)
    {
        var exists = await db.Set<Tenant>()
            .IgnoreQueryFilters()
            .AnyAsync(item => item.Id == tenantId);

        if (exists)
            return;

        db.Set<Tenant>().Add(new Tenant
        {
            Id = tenantId,
            TenantId = tenantId,
            Name = name,
            Description = "Attendance integration test tenant",
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        });
    }

    private static async Task SeedAttendanceFeature(AppDbContext db, Guid tenantId)
    {
        var exists = await db.Set<TenantModuleFeature>()
            .IgnoreQueryFilters()
            .AnyAsync(item =>
                item.TenantId == tenantId &&
                item.ModuleKey == TenantModuleFeatureKeys.Attendance &&
                item.SubFeatureKey == string.Empty);

        if (exists)
            return;

        db.Set<TenantModuleFeature>().Add(new TenantModuleFeature
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleKey = TenantModuleFeatureKeys.Attendance,
            SubFeatureKey = string.Empty,
            DisplayName = "Attendance",
            Description = "Attendance contexts, sessions, participants, time events, and reports.",
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        });
    }

    private static void ForceModelAssemblies()
    {
        RuntimeHelpers.RunClassConstructor(typeof(AttendanceContext).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(IdentityCredential).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(TenantModuleFeature).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(Wallets.Domain.Shared.Contracts.WalletType).TypeHandle);
    }

    private sealed class AttendanceIntegrationCredentialResolver : IAttendanceCredentialResolver
    {
        public Task<Result<AttendanceCredentialSnapshot>> ResolveAsync(
            Guid credentialId,
            Guid tenantId,
            CancellationToken ct) =>
            Task.FromResult(Result<AttendanceCredentialSnapshot>.Success(new(
                credentialId,
                tenantId,
                true,
                false,
                $"Credential {credentialId:N}",
                credentialId.ToString("N"))));
    }
}
