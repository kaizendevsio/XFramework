using Bolt.Client;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Bolt.Hub.Extensions;
using Payments.Core;
using Testcontainers.PostgreSql;
using Wallets.Api.Events;
using Wallets.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Enums;
using Wallets.Integration.Drivers;
using XFramework.Core.DataContext;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Domain.Contexts;
using XFramework.Domain.Interceptors;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Extensions;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Extensions;
using BatchDecrementEndpoint = Wallets.Api.Features.Batch.DecrementBatch.Endpoint;
using BatchIncrementEndpoint = Wallets.Api.Features.Batch.IncrementBatch.Endpoint;
using BatchTransferEndpoint = Wallets.Api.Features.Batch.TransferBatch.Endpoint;
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
        await WaitForHealth($"{BoltUrl}/health/live", _streamFlowTask);

        _walletsApp = StartWallets();
        await WaitForHealth($"{WalletsUrl}/health/live", _walletsTask);

        var cache = _walletsApp.Services.GetRequiredService<IMemoryCache>();
        cache.Set($"GetTenant-{TestTenantId}", new Contracts.Tenant
        {
            Id = TestTenantId, TenantId = TestTenantId,
            Name = "Test Tenant", Description = "Wallets integration test tenant"
        });

        _testClientApp = StartTestClient();
        await WaitForHealth($"{TestClientUrl}/health/live", _testClientTask);

        await WaitForBoltClients();
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
        // Build manually so test configuration is in place before installers/Bolt client registration read it.
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(WalletsUrl);
        OverrideConfig(builder, "Wallets", "4902761a-822d-4c6b-8e2d-323fd501bcd6");
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

        builder.Services.InstallStandardServices<Wallets.Api.Services.WalletOperationsService>(builder.Configuration);
        builder.Services.AddHttpClient();
        builder.Services.AddPaymentServices();
        builder.Services.AddTenantResolver();
        builder.Services.AddTenantModuleFeatures();
        builder.Services.AddAuthentication("WalletsTest")
            .AddScheme<AuthenticationSchemeOptions, WalletsTestAuthHandler>("WalletsTest", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IWalletEventPublisher, WalletEventPublisher>();
        builder.Services.AddScoped<Wallets.Api.Services.IWalletFeatureGateService, Wallets.Api.Services.WalletFeatureGateService>();
        builder.Services.AddScoped<Wallets.Api.Services.IWalletRequestContextResolver, Wallets.Api.Services.WalletRequestContextResolver>();
        builder.Services.AddScoped<Wallets.Api.Services.IWalletFeeCalculator, Wallets.Api.Services.WalletFeeCalculator>();
        builder.Services.AddScoped<Wallets.Api.Services.IWalletPolicyEvaluator, Wallets.Api.Services.WalletPolicyEvaluator>();
        builder.Services.AddScoped<Wallets.Api.Services.IWalletLedgerService, Wallets.Api.Services.WalletLedgerService>();
        builder.Services.AddScoped<Wallets.Api.Services.IWalletOperationsService, Wallets.Api.Services.WalletOperationsService>();
        builder.Services.AddScoped<Wallets.Api.Services.IBatchWalletService, Wallets.Api.Services.BatchWalletService>();
        builder.Services.AddScoped<Wallets.Api.Services.IWalletWorkflowService, Wallets.Api.Services.WalletWorkflowService>();
        builder.Services.AddScoped<Wallets.Api.Services.IWalletApprovalWorkflowService, Wallets.Api.Services.WalletWorkflowService>();
        builder.Services.AddScoped<Wallets.Api.Services.IWalletCaseWorkflowService, Wallets.Api.Services.WalletWorkflowService>();
        builder.Services.AddScoped<Wallets.Api.Services.IWalletReportingService, Wallets.Api.Services.WalletWorkflowService>();
        builder.Services.AddScoped<Wallets.Api.Services.IWalletPolicyAdminService, Wallets.Api.Services.WalletPolicyAdminService>();
        builder.Services.AddScoped<Wallets.Api.Services.IWalletPaymentWebhookService, Wallets.Api.Services.WalletPaymentWebhookService>();
        builder.Services.AddScoped<Wallets.Api.Services.IWalletOutboxService, Wallets.Api.Services.WalletOutboxService>();
        builder.Services.AddScoped<Wallets.Api.Services.IWalletOutboxPublisher, Wallets.Api.Services.WalletOutboxPublisher>();
        builder.Services.AddScoped<Wallets.Api.Services.IWalletReconciliationService, Wallets.Api.Services.WalletReconciliationService>();
        builder.Services.AddValidatorsFromAssemblyContaining<Wallets.Api.Services.IWalletOperationsService>();
        builder.Services.AddXFrameworkBoltClient(builder.Configuration, autoConnect: false);
        builder.Services.AddDataContextHandler(typeof(Wallets.Api.Services.WalletOperationsService).Assembly);

        var app = (WebApplication)builder.Build();
        RegisterWalletsBoltHandlers(app);
        app.UseCorrelationId();
        app.UseAuthentication();
        app.UseAuthorization();

        // Map source-generated endpoints
        Wallets.Api.Generated.GeneratedEndpointRoutes.MapGeneratedEndpoints(app);

        // Manual endpoints
        Wallets.Api.Features.Wallets.Get.GetWalletEndpoint.Map(app);
        Wallets.Api.Features.Wallets.GetByCredential.GetWalletsByCredentialEndpoint.Map(app);
        BatchIncrementEndpoint.MapBatchIncrementEndpoint(app);
        BatchDecrementEndpoint.MapBatchDecrementEndpoint(app);
        BatchTransferEndpoint.MapBatchTransferEndpoint(app);

        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _walletsTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static void RegisterWalletsBoltHandlers(WebApplication app)
    {
        var client = app.Services.GetRequiredService<BoltClient>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Wallets.GeneratedBoltHandlers");
        var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

        Wallets.Api.Generated.BoltHandlerRegistry.RegisterAll(client, logger, scopeFactory);
    }

    private static WebApplication StartTestClient()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(TestClientUrl);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BoltConfiguration:ClientName"] = "WalletTestClient",
            ["BoltConfiguration:ClientGuid"] = Guid.NewGuid().ToString(),
            ["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/bolt/ws",
            ["BoltConfiguration:Signature"] = "wallets-bolt-test-secret",
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Logging:LogLevel:Default"] = "Warning",
        });

        // NOTE(Task13): Test client uses thin-protocol BoltDriver. Wallets service still uses
        // SignalR for handler registration, so StreamFlow tests will time out until Task 13
        // updates the source generator to emit thin-protocol handlers.
        builder.Services.InstallStandardServices<WalletsTestFixture>(builder.Configuration);
        builder.Services.AddSingleton(new DeviceAgentProvider("WalletTest"));
        builder.Services.AddXFrameworkBoltClient(builder.Configuration, autoConnect: false);
        builder.Services.AddWalletsWrapperServices();

        var app = builder.Build();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _testClientTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static async Task WaitForBoltClients()
    {
        var walletsClient = _walletsApp.Services.GetRequiredService<BoltClient>();
        var testClient = _testClientApp.Services.GetRequiredService<BoltClient>();

        // Handler registration is now automatic via BoltHandlerRegistrationHostedService
        // when AddXFrameworkBoltClient() is called in the service's startup.
        // Give the BoltClient time to connect and the hosted service to register handlers.
        await ConnectBoltClient(walletsClient, "Wallets service");
        await ConnectBoltClient(testClient, "Wallets test client");
        await Task.Delay(1000);

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (walletsClient.IsConnected && testClient.IsConnected)
            {
                await Task.Delay(1000);
                return;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException("Wallets Bolt clients failed to connect within 15s");
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
            ["BoltConfiguration:Signature"] = "wallets-bolt-test-secret",
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Wallets:Webhooks:SharedSecret"] = "wallets-webhook-test-secret",
            ["Logging:LogLevel:Default"] = "Warning"
        });
    }

    private static async Task MigrateAndSeed()
    {
        // AppDbContext discovers mappings from already-loaded Domain.Shared assemblies.
        // Force the module contracts used by this suite before constructing the context.
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(Wallets.Domain.Shared.Contracts.WalletType).TypeHandle);
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(Contracts.IdentityCredential).TypeHandle);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            // The shared migration snapshot contains modules outside this test load graph.
            // Match the migration runner behavior while still loading Wallets/Identity mappings above.
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        await XFramework.TestInfrastructure.TestSeedData.SeedAll(db);
        await SeedWalletFeeSchedules(db);
    }

    private static async Task SeedWalletFeeSchedules(AppDbContext db)
    {
        var operations = new[]
        {
            WalletOperationType.Credit,
            WalletOperationType.Debit,
            WalletOperationType.Transfer,
            WalletOperationType.Conversion,
            WalletOperationType.Release,
            WalletOperationType.Reversal,
            WalletOperationType.Hold,
            WalletOperationType.DepositApproval,
            WalletOperationType.WithdrawalApproval,
            WalletOperationType.Refund,
            WalletOperationType.DisputeResolution,
            WalletOperationType.Chargeback
        };

        foreach (var operation in operations)
        {
            db.Set<WalletFeeSchedule>().Add(new WalletFeeSchedule
            {
                Id = Guid.NewGuid(),
                TenantId = TestTenantId,
                Name = $"Integration fee override {operation}",
                OperationType = operation,
                WalletTypeId = TestWalletTypeId,
                FixedFee = 0,
                PercentageFee = 0,
                AllowRequestedFeeOverride = true,
                EffectiveAt = DateTime.UtcNow.AddDays(-1),
                IsEnabled = true
            });
        }

        await db.SaveChangesAsync();
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

            if (appTask is { IsCompleted: true })
                throw new InvalidOperationException("Application stopped before the health endpoint became available.");

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

        if (appTask is { IsCompleted: true })
            throw new InvalidOperationException("Application stopped before the health endpoint became available.");

        throw new TimeoutException($"Service at {url} did not become healthy within {timeoutSeconds}s");
    }

    private sealed class WalletsTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var tenantId = Request.Headers.TryGetValue("X-Wallets-Test-TenantId", out var tenantHeader) &&
                           Guid.TryParse(tenantHeader.FirstOrDefault(), out var suppliedTenantId)
                ? suppliedTenantId
                : TestTenantId;
            var credentialId = Request.Headers.TryGetValue("X-Wallets-Test-CredentialId", out var credentialHeader) &&
                               Guid.TryParse(credentialHeader.FirstOrDefault(), out var suppliedCredentialId)
                ? suppliedCredentialId
                : (Guid?)null;
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "wallets-test"),
                new Claim("tenantId", tenantId.ToString()),
                new Claim("TenantId", tenantId.ToString()),
                new Claim("tid", tenantId.ToString())
            };

            if (credentialId.HasValue)
            {
                claims.Add(new Claim("credential_id", credentialId.Value.ToString()));
            }

            var suppressDefaultRole = Request.Headers.TryGetValue("X-Wallets-Test-No-Role", out var noRoleHeader) &&
                                      bool.TryParse(noRoleHeader.FirstOrDefault(), out var noRole) &&
                                      noRole;
            if (Request.Headers.TryGetValue("X-Wallets-Test-Role", out var roleHeader))
            {
                foreach (var role in roleHeader.Where(static x => !string.IsNullOrWhiteSpace(x)))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role!));
                }
            }
            else if (!suppressDefaultRole)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
