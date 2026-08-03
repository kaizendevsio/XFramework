using Bolt.Client;
using FluentValidation;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Api.Features.Credentials.Update;
using IdentityServer.Api.Features.Verification.Confirm;
using IdentityServer.Api.Features.GeneratedEntityValidation;
using IdentityServer.Api.Generated;
using IdentityServer.Api.Infrastructure;
using IdentityServer.Api.Services;
using IdentityServer.Api.Features.Tenants;
using IdentityServer.Integration.Drivers;
using Communications.Integration.Drivers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using Bolt.Hub.Extensions;
using Storage.Domain.Shared.Contracts.Requests;
using Storage.Domain.Shared.Contracts.Responses;
using Storage.Integration.Drivers;
using Testcontainers.PostgreSql;
using XFramework.Core.DataContext;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Core.Patterns;
using XFramework.Core.RateLimiting;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Configurations;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Extensions;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Extensions;
using XFramework.Integration.Extensions;
using XFramework.Integration.Drivers;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;
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
    public static string BoltUrl { get; } = GetAvailableLoopbackUrl();
    public static string IdentityServerUrl { get; } = GetAvailableLoopbackUrl();
    public static string TestClientUrl { get; } = GetAvailableLoopbackUrl();

    public static IServiceProvider Services => _identityServerApp.Services;
    private const string TestServiceClientId = "TestClient";
    private const string TestServiceGenerationId = "test-client-g1";
    private const string TestServiceClientSecret = "IdentityServerIntegrationTestSecret-2026";
    private const string LimitedScopeClientId = "LimitedIdentityClient";
    private const string LimitedScopeGenerationId = "limited-identity-client-g1";
    private const string LimitedScopeClientSecret = "LimitedIdentityIntegrationSecret-2026";
    private const string TestHostServiceGenerationId = TestServiceGenerationId;
    private const string TestHostServiceClientSecret = "IdentityServerHostIntegrationSecret-2026";
    private static readonly string TransportSigningKeyDirectory = Path.Combine(
        Path.GetTempPath(),
        "XFramework.IdentityServer.IntegrationTests",
        Guid.NewGuid().ToString("N"));
    private static readonly string TransportSigningKeyPath = Path.Combine(
        TransportSigningKeyDirectory,
        "bolt-transport-signing-key.pem");
    private static readonly string UserJwtPrivateKeyPath = Path.Combine(
        TransportSigningKeyDirectory,
        "user-jwt-private-key.pem");
    private static readonly string UserJwtPublicKeyPath = Path.Combine(
        TransportSigningKeyDirectory,
        "user-jwt-public-key.pem");

    /// <summary>
    /// Service wrapper that calls IdentityServer through the actual Bolt transport.
    /// </summary>
    public static IIdentityServerServiceWrapper ServiceWrapper =>
        _testClientApp.Services.GetRequiredService<IIdentityServerServiceWrapper>();

    public static async Task<IIdentityServerServiceWrapper> CreateLimitedScopeServiceWrapper()
    {
        using var client = new HttpClient { BaseAddress = new Uri(IdentityServerUrl) };
        using var response = await client.PostAsJsonAsync(
            "/api/service-identity/token",
            new IssueServiceTokenRequest
            {
                ClientId = LimitedScopeClientId,
                ClientSecret = LimitedScopeClientSecret,
                Audience = XFrameworkServiceNames.IdentityServer,
                Scopes = [XFrameworkServiceScopes.DataContextQuery]
            });
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<ServiceTokenResponse>()
            ?? throw new InvalidOperationException("Limited-scope service token response was empty.");
        var tokenProvider = new FixedServiceTokenProvider(token.AccessToken);
        var driver = new BoltDriver(
            _testClientApp.Services.GetRequiredService<BoltClient>(),
            _testClientApp.Services.GetRequiredService<IOptions<BoltConfiguration>>(),
            tokenProvider,
            _testClientApp.Services.GetRequiredService<ILogger<BoltDriver>>());

        return ActivatorUtilities.CreateInstance<IdentityServerServiceWrapper>(
            _testClientApp.Services,
            driver,
            tokenProvider);
    }

    /// <summary>
    /// RemoteDataContext that queries IdentityServer through the Bolt transport.
    /// Creates a new scope each call so tests get a fresh context.
    /// </summary>
    public static IDataContext DataContext =>
        _testClientApp.Services.CreateScope().ServiceProvider.GetRequiredService<IDataContext>();

    public static readonly Guid TestTenantId = Guid.Parse("7602c2d3-01df-4bdb-9a67-02c144e4a2ac");
    public static readonly Guid TestCredentialId = Guid.Parse("00000000-0000-0000-0000-000000000691");

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
        await VerifyCentralTransportIdentity();

        // 5. Seed the in-memory tenant cache
        var cache = _identityServerApp.Services.GetRequiredService<IMemoryCache>();
        cache.Set($"GetTenant-{TestTenantId}", new Contracts.Tenant
        {
            Id = TestTenantId,
            TenantId = TestTenantId,
            Name = "Test Tenant",
            Description = "Integration test tenant",
            IsEnabled = true
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
        if (Directory.Exists(TransportSigningKeyDirectory))
            Directory.Delete(TransportSigningKeyDirectory, recursive: true);
    }

    private static WebApplication StartBolt()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseUrls(BoltUrl);
        OverrideConfiguration(builder, "Bolt.Test", "00000000-0000-0000-0000-000000000001", BoltUrl);
        builder.Services.InstallServicesInAssembly<Bolt.Hub.Installers.BoltInstaller>(builder.Configuration, builder.Environment);
        builder.Services.InstallSwagger(builder.Configuration);
        builder.Services.InstallOData(builder.Configuration);
        builder.Services.InstallJwt(builder.Configuration, builder.Environment);
        builder.Services.InstallStandardServices<Bolt.Hub.Installers.BoltInstaller>(builder.Configuration);
        builder.Services.InstallRuntimeServices(builder.Configuration);

        var app = (WebApplication)builder.Build();
        app.UseCorrelationId();
        app.UseAppServices();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        StartApplication(app);
        _streamFlowTask = app.WaitForShutdownAsync();
        return app;
    }

    private static WebApplication StartIdentityServer()
    {
        // Build manually (instead of XApplication.Configure<AuthService>()) so that config
        // overrides are applied BEFORE the installers read BoltConfiguration to create BoltClient.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseUrls(IdentityServerUrl);

        // Override configuration first — installers read config at registration time
        OverrideConfiguration(builder, XFrameworkServiceNames.IdentityServer, Guid.NewGuid().ToString(), IdentityServerUrl);
        builder.Configuration["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/bolt/ws";

        // Register services that the installers normally provide, except WrapperInstaller
        // (WrapperInstaller calls AddXFrameworkBoltClient which reads ClientGuid as Guid?,
        // but the service ID is a SHA256 hex string — not a valid GUID).
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<DbCommandCounterInterceptor>();
        builder.Services.AddDbContext<DbContext, AppDbContext>((sp, options) => options
            .UseNpgsql(ConnectionString,
                npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .AddInterceptors(sp.GetRequiredService<DbCommandCounterInterceptor>())
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.BoolWithDefaultWarning)));
        builder.Services.AddServerDataContext<AppDbContext>();
        XFramework.GeneratedServices.GeneratedEntityServiceRegistrations
            .AddGeneratedEntityServices(builder.Services);

        builder.Services.InstallJwt(builder.Configuration, builder.Environment);
        builder.Services.InstallStandardServices<AuthService>(builder.Configuration);
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "IdentityServerTest";
                options.DefaultChallengeScheme = "IdentityServerTest";
                options.DefaultScheme = "IdentityServerTest";
            })
            .AddScheme<AuthenticationSchemeOptions, IdentityServerTestAuthHandler>("IdentityServerTest", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
        builder.Services.AddTenantResolver();
        builder.Services.AddTenantModuleFeatures();
        builder.Services.AddSingleton(
            SuccessCommunicationsServiceWrapperProxy.Create());
        builder.Services.AddScoped<IStorageServiceWrapper, TestStorageServiceWrapper>();
        builder.Services.AddSingleton<TestDistributedSecurityRateLimiter>();
        builder.Services.AddSingleton<IDistributedSecurityRateLimiter>(serviceProvider =>
            serviceProvider.GetRequiredService<TestDistributedSecurityRateLimiter>());
        builder.Services.AddHostedService<PasswordResetOutboxDispatcher>();
        builder.Services.AddHostedService<VerificationDeliveryOutboxDispatcher>();
        builder.Services.AddHostedService<StorageCleanupOutboxDispatcher>();
        builder.Services.AddHostedService<StorageClaimOutboxDispatcher>();
        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<IAuthService>(serviceProvider => serviceProvider.GetRequiredService<AuthService>());
        builder.Services.AddScoped<IPasswordResetProcessor>(serviceProvider =>
            new FailureInjectingPasswordResetProcessor(serviceProvider.GetRequiredService<AuthService>()));
        builder.Services.AddScoped<IIdentityAuthorizationService, IdentityAuthorizationService>();
        builder.Services.AddScoped<IIdentityAdministrationService, IdentityAdministrationService>();
        builder.Services.AddScoped<ITenantAdministrationService, TenantAdministrationService>();
        builder.Services.AddScoped<IServiceIdentityService, ServiceIdentityService>();
        builder.Services.AddSingleton(serviceProvider => ServiceIdentityConfiguration.FromConfiguration(
            serviceProvider.GetRequiredService<IConfiguration>(),
            serviceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
            serviceProvider.GetRequiredService<IHostEnvironment>().EnvironmentName));
        builder.Services.AddSingleton<IBoltTransportTokenSigner, FileBackedBoltTransportTokenSigner>();
        builder.Services.AddSingleton<IBoltTransportTokenProvider>(serviceProvider =>
            new IntegrationBoltTransportTokenProvider(
                serviceProvider.GetRequiredService<IBoltTransportTokenSigner>(),
                XFrameworkServiceNames.IdentityServer,
                TestHostServiceGenerationId));
        builder.Services.AddSingleton<IIdentitySigningKeyProvider, IdentityServerLocalSigningKeyProvider>();
        builder.Services.AddValidatorsFromAssemblyContaining<AuthService>();
        builder.Services.AddIdentityServerRemoteEntityValidation();
        builder.Services.AddXFrameworkBoltClient(builder.Configuration, autoConnect: false);

        // Register DataContext handler so IdentityServer can serve __db_query__/__db_changes__ via Bolt
        builder.Services.AddDataContextHandler(typeof(AuthService).Assembly);

        var app = (WebApplication)builder.Build();
        _ = app.Services.GetRequiredService<ServiceIdentityConfiguration>();
        RegisterIdentityServerBoltHandlers(app);
        app.UseCorrelationId();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseTenantModuleFeatureGate(IdentityServerFeatureGateRoutes.Configure);
        app.MapGeneratedEndpoints();
        XFramework.GeneratedEndpoints.GeneratedEntityEndpointRoutes.MapGeneratedEntityEndpoints(app);
        app.MapConfirmVerificationEndpoint();
        app.MapUpdateCredentialEndpoint();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        StartApplication(app);
        _identityServerTask = app.WaitForShutdownAsync();
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
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseUrls(TestClientUrl);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BoltConfiguration:ClientName"] = "TestClient",
            ["BoltConfiguration:ClientGuid"] = Guid.NewGuid().ToString(),
            ["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/bolt/ws",
            ["BoltConfiguration:GenerateServiceAccessToken"] = "false",
            ["ServiceIdentity:ClientId"] = TestServiceClientId,
            ["ServiceIdentity:Authority"] = IdentityServerUrl,
            ["ServiceIdentity:AllowInsecureHttp"] = "true",
            ["ServiceIdentity:GenerationId"] = TestServiceGenerationId,
            ["ServiceIdentity:ClientSecret"] = TestServiceClientSecret,
            ["ServiceIdentity:DefaultScopes:0"] = XFrameworkServiceScopes.BoltService,
            ["ServiceIdentity:DefaultScopes:1"] = XFrameworkServiceScopes.IdentityAdmin,
            ["ServiceIdentity:DefaultScopes:2"] = XFrameworkServiceScopes.DataContextQuery,
            ["ServiceIdentity:DefaultScopes:3"] = XFrameworkServiceScopes.DataContextMutate,
            ["ServiceIdentity:DefaultScopes:4"] = XFrameworkServiceScopes.IdentitySessionValidate,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Kestrel:Endpoints:Http:Url"] = TestClientUrl,
            ["urls"] = TestClientUrl,
            ["JwtOptions:ValidAudience"] = "http://localhost:18261",
            ["JwtOptions:ValidIssuer"] = "http://localhost:18261",
            ["JwtOptions:GenerationId"] = "test-jwt-g1",
            ["JwtOptions:SigningPublicKeyPath"] = UserJwtPublicKeyPath,
            ["JwtOptions:SigningPrivateKeyPath"] = string.Empty,
            ["JwtOptions:AccessTokenLifespan"] = "00:30:00",
            ["JwtOptions:RefreshTokenLifespan"] = "00:30:00",
            ["Logging:LogLevel:Default"] = "Warning",
        });

        // Core services needed by service wrappers.
        // NOTE(Task13): Test client uses thin-protocol BoltDriver. IdentityServer still uses
        // SignalR for handler registration, so StreamFlow tests will time out until Task 13
        // updates the source generator to emit thin-protocol handlers.
        builder.Services.InstallJwt(builder.Configuration, builder.Environment);
        builder.Services.InstallStandardServices<IntegrationTestFixture>(builder.Configuration);
        builder.Services.AddSingleton(new XFramework.Domain.Shared.BusinessObjects.DeviceAgentProvider("IntegrationTest"));
        builder.Services.AddSingleton<IBoltTransportTokenProvider>(_ =>
            new IntegrationBoltTransportTokenProvider(
                _identityServerApp.Services.GetRequiredService<IBoltTransportTokenSigner>(),
                TestServiceClientId,
                TestServiceGenerationId));
        builder.Services.AddXFrameworkBoltClient(builder.Configuration, autoConnect: false);

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

    private static string GetAvailableLoopbackUrl()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return $"http://127.0.0.1:{port}";
    }

    private static async Task WaitForBoltClients()
    {
        var identityServerClient = _identityServerApp.Services.GetRequiredService<BoltClient>();
        var testClient = _testClientApp.Services.GetRequiredService<BoltClient>();

        await ConnectBoltClient(identityServerClient, "IdentityServer service");
        await ConnectBoltClient(testClient, "IdentityServer test client");
        await Task.Delay(1000);

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (identityServerClient.IsConnected && testClient.IsConnected)
            {
                await Task.Delay(1000);
                return;
            }
            await Task.Delay(250);
        }

        throw new TimeoutException("IdentityServer Bolt clients failed to connect within 15s");
    }

    private static async Task VerifyCentralTransportIdentity()
    {
        using var client = new HttpClient { BaseAddress = new Uri(IdentityServerUrl) };
        using var metadata = await client.GetAsync(BoltTransportTokenConstants.MetadataPath);
        metadata.EnsureSuccessStatusCode();
        using var jwks = await client.GetAsync(BoltTransportTokenConstants.JsonWebKeySetPath);
        jwks.EnsureSuccessStatusCode();
        using var token = await client.PostAsJsonAsync(
            BoltTransportTokenConstants.TokenEndpointPath,
            new
            {
                ClientId = XFrameworkServiceNames.IdentityServer,
                ClientSecret = TestHostServiceClientSecret
            });
        token.EnsureSuccessStatusCode();
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

    private sealed class IntegrationBoltTransportTokenProvider(
        IBoltTransportTokenSigner signer,
        string clientId,
        string generationId) : IBoltTransportTokenProvider
    {
        public ValueTask<string> GetTokenAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var issuedAt = DateTimeOffset.UtcNow.AddSeconds(-5);
            return ValueTask.FromResult(signer.Sign(
                clientId,
                generationId,
                issuedAt,
                issuedAt.AddMinutes(30)));
        }
    }

    private static void OverrideConfiguration(
        WebApplicationBuilder builder,
        string clientName,
        string clientGuid,
        string serverUrl)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultDatabaseConnection"] = ConnectionString,
            ["DefaultDatabaseConnection"] = ConnectionString,
            ["BoltConfiguration:ClientGuid"] = clientGuid,
            ["BoltConfiguration:ClientName"] = clientName,
            ["BoltConfiguration:GenerateServiceAccessToken"] = "false",
            ["ServiceIdentity:ClientId"] = clientName,
            ["ServiceIdentity:Authority"] = IdentityServerUrl,
            ["ServiceIdentity:AllowInsecureHttp"] = "true",
            ["ServiceIdentity:GenerationId"] = TestHostServiceGenerationId,
            ["ServiceIdentity:ClientSecret"] = TestHostServiceClientSecret,
            ["ServiceIdentity:DefaultScopes:0"] = XFrameworkServiceScopes.BoltService,
            ["ServiceIdentity:DefaultScopes:1"] = XFrameworkServiceScopes.StorageRead,
            ["ServiceIdentity:DefaultScopes:2"] = XFrameworkServiceScopes.StorageWrite,
            ["ServiceIdentity:BoltTransportTokenIssuer:Enabled"] = "true",
            ["ServiceIdentity:BoltTransportTokenIssuer:SigningKeyPath"] = TransportSigningKeyPath,
            ["ServiceIdentity:Clients:0:ClientId"] = TestServiceClientId,
            ["ServiceIdentity:Clients:0:GenerationId"] = TestServiceGenerationId,
            ["ServiceIdentity:Clients:0:ClientSecret"] = TestServiceClientSecret,
            ["ServiceIdentity:Clients:0:AllowedAudiences"] = XFrameworkServiceNames.IdentityServer,
            ["ServiceIdentity:Clients:0:AllowedScopes"] = string.Join(',',
                XFrameworkServiceScopes.BoltService,
                XFrameworkServiceScopes.IdentityAdmin,
                XFrameworkServiceScopes.IdentitySessionValidate,
                XFrameworkServiceScopes.DataContextQuery,
                XFrameworkServiceScopes.DataContextQueryAllTenants,
                XFrameworkServiceScopes.DataContextMutate),
            ["ServiceIdentity:Clients:1:ClientId"] = clientName,
            ["ServiceIdentity:Clients:1:GenerationId"] = TestHostServiceGenerationId,
            ["ServiceIdentity:Clients:1:ClientSecret"] = TestHostServiceClientSecret,
            ["ServiceIdentity:Clients:1:AllowedAudiences"] = string.Join(',',
                XFrameworkServiceNames.IdentityServer,
                XFrameworkServiceNames.Storage,
                XFrameworkServiceNames.Communications),
            ["ServiceIdentity:Clients:1:AllowedScopes"] = string.Join(',',
                XFrameworkServiceScopes.BoltService,
                XFrameworkServiceScopes.StorageRead,
                XFrameworkServiceScopes.StorageWrite),
            ["ServiceIdentity:Clients:2:ClientId"] = LimitedScopeClientId,
            ["ServiceIdentity:Clients:2:GenerationId"] = LimitedScopeGenerationId,
            ["ServiceIdentity:Clients:2:ClientSecret"] = LimitedScopeClientSecret,
            ["ServiceIdentity:Clients:2:AllowedAudiences"] = XFrameworkServiceNames.IdentityServer,
            ["ServiceIdentity:Clients:2:AllowedScopes"] = XFrameworkServiceScopes.DataContextQuery,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Kestrel:Endpoints:Http:Url"] = serverUrl,
            ["urls"] = serverUrl,
            ["JwtOptions:ValidAudience"] = "http://localhost:18261",
            ["JwtOptions:ValidIssuer"] = "http://localhost:18261",
            ["JwtOptions:GenerationId"] = "test-jwt-g1",
            ["JwtOptions:SigningPublicKeyPath"] = UserJwtPublicKeyPath,
            ["JwtOptions:SigningPrivateKeyPath"] = UserJwtPrivateKeyPath,
            ["JwtOptions:AccessTokenLifespan"] = "00:30:00",
            ["JwtOptions:RefreshTokenLifespan"] = "00:30:00",
            ["BoltTransportAuthentication:MetadataAddress"] =
                $"{IdentityServerUrl}{BoltTransportTokenConstants.MetadataPath}",
            ["BoltTransportAuthentication:Issuer"] = XFrameworkServiceNames.IdentityServer,
            ["BoltTransportAuthentication:Audience"] = XFrameworkServiceNames.BoltHub,
            ["BoltTransportAuthentication:RequireHttpsMetadata"] = "false",
            ["Logging:LogLevel:Default"] = "Warning"
        });
    }

    private static async Task MigrateAndSeed()
    {
        TestDatabaseModel.LoadMigrationAssemblies();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        await XFramework.TestInfrastructure.TestSeedData.SeedAll(db);
        await SeedAuthenticatedTestCredential(db);
    }

    private static async Task SeedAuthenticatedTestCredential(AppDbContext db)
    {
        if (await db.Set<Contracts.IdentityCredential>()
                .IgnoreQueryFilters()
                .AnyAsync(credential => credential.Id == TestCredentialId))
        {
            return;
        }

        var identityId = Guid.Parse("00000000-0000-0000-0000-000000000690");
        db.Set<Contracts.IdentityInformation>().Add(new Contracts.IdentityInformation
        {
            Id = identityId,
            TenantId = TestTenantId,
            IdentityName = "IdentityServer integration administrator",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        });
        db.Set<Contracts.IdentityCredential>().Add(new Contracts.IdentityCredential
        {
            Id = TestCredentialId,
            TenantId = TestTenantId,
            IdentityInfoId = identityId,
            UserName = "identityserver-test-admin",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        });
        db.Set<Contracts.IdentityRole>().Add(new Contracts.IdentityRole
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000692"),
            TenantId = TestTenantId,
            CredentialId = TestCredentialId,
            TypeId = TestConstants.RoleTypeId,
            RoleExpiration = DateTime.UtcNow.AddYears(10),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        });
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
        {
            throw new InvalidOperationException("Application stopped before the health endpoint became available.");
        }

        throw new TimeoutException($"Service at {url} did not become healthy within {timeoutSeconds}s");
    }

    private sealed class FixedServiceTokenProvider(string accessToken) : IServiceTokenProvider
    {
        public ValueTask<string> GetTokenAsync(
            string audience,
            IReadOnlyCollection<string>? scopes = null,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult(accessToken);
        }
    }
}

internal class SuccessCommunicationsServiceWrapperProxy : DispatchProxy
{
    public static ICommunicationsServiceWrapper Create() =>
        DispatchProxy.Create<ICommunicationsServiceWrapper, SuccessCommunicationsServiceWrapperProxy>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == nameof(ICommunicationsServiceWrapper.CreateDirectMessageAsync))
        {
            IdentityServerWorkflowFailureInjection.RecordCommunicationsDeliveryAttempt();
            if (IdentityServerWorkflowFailureInjection.FailCommunicationsDelivery)
            {
                return Task.FromException<CmdResponse>(
                    new IOException("Injected ambiguous communications failure"));
            }

            var reject = IdentityServerWorkflowFailureInjection.ConsumeCommunicationsRejection();
            return Task.FromResult(new CmdResponse
            {
                HttpStatusCode = reject
                    ? HttpStatusCode.BadGateway
                    : HttpStatusCode.Accepted,
                Message = reject ? "Injected communications rejection" : null
            });
        }

        throw new NotSupportedException(
            $"Communications method '{targetMethod?.Name}' is not configured for IdentityServer integration tests.");
    }

}

public enum TestStorageFailurePoint
{
    None,
    UploadPart,
    CancelUploadPart,
    CompleteUpload,
    CredentialAvatarPersistence
}

internal static class IdentityServerWorkflowFailureInjection
{
    private static int _storageFailurePoint;
    private static int _failCommunicationsDelivery;
    private static int _failNextStorageDelete;
    private static int _abortedUploadCount;
    private static int _deletedFileCount;
    private static int _remainingStorageClaimFailures;
    private static int _storageClaimAttemptCount;
    private static int _remainingCommunicationsRejections;
    private static int _remainingPasswordResetProcessorFailures;
    private static int _communicationsDeliveryAttemptCount;

    public static TestStorageFailurePoint StorageFailurePoint
    {
        get => (TestStorageFailurePoint)Volatile.Read(ref _storageFailurePoint);
        set => Volatile.Write(ref _storageFailurePoint, (int)value);
    }

    public static bool FailCommunicationsDelivery
    {
        get => Volatile.Read(ref _failCommunicationsDelivery) == 1;
        set => Volatile.Write(ref _failCommunicationsDelivery, value ? 1 : 0);
    }

    public static bool FailNextStorageDelete
    {
        get => Volatile.Read(ref _failNextStorageDelete) == 1;
        set => Volatile.Write(ref _failNextStorageDelete, value ? 1 : 0);
    }

    public static int AbortedUploadCount => Volatile.Read(ref _abortedUploadCount);
    public static int DeletedFileCount => Volatile.Read(ref _deletedFileCount);
    public static int StorageClaimAttemptCount => Volatile.Read(ref _storageClaimAttemptCount);
    public static int CommunicationsDeliveryAttemptCount => Volatile.Read(ref _communicationsDeliveryAttemptCount);

    public static void RecordAbort() => Interlocked.Increment(ref _abortedUploadCount);
    public static void RecordDelete() => Interlocked.Increment(ref _deletedFileCount);
    public static void RecordStorageClaim() => Interlocked.Increment(ref _storageClaimAttemptCount);
    public static void RejectNextStorageClaims(int count = 1) =>
        Volatile.Write(ref _remainingStorageClaimFailures, count);
    public static bool ConsumeStorageClaimFailure() => ConsumeOne(ref _remainingStorageClaimFailures);
    public static void RecordCommunicationsDeliveryAttempt() =>
        Interlocked.Increment(ref _communicationsDeliveryAttemptCount);

    public static void RejectNextCommunicationsDeliveries(int count) =>
        Volatile.Write(ref _remainingCommunicationsRejections, count);

    public static void FailNextPasswordResetProcessing(int count = 1) =>
        Volatile.Write(ref _remainingPasswordResetProcessorFailures, count);

    public static bool ConsumeCommunicationsRejection() => ConsumeOne(ref _remainingCommunicationsRejections);
    public static bool ConsumePasswordResetProcessorFailure() =>
        ConsumeOne(ref _remainingPasswordResetProcessorFailures);

    public static void Reset()
    {
        StorageFailurePoint = TestStorageFailurePoint.None;
        FailCommunicationsDelivery = false;
        FailNextStorageDelete = false;
        Volatile.Write(ref _abortedUploadCount, 0);
        Volatile.Write(ref _deletedFileCount, 0);
        Volatile.Write(ref _remainingStorageClaimFailures, 0);
        Volatile.Write(ref _storageClaimAttemptCount, 0);
        Volatile.Write(ref _remainingCommunicationsRejections, 0);
        Volatile.Write(ref _remainingPasswordResetProcessorFailures, 0);
        Volatile.Write(ref _communicationsDeliveryAttemptCount, 0);
    }

    private static bool ConsumeOne(ref int remaining)
    {
        while (true)
        {
            var current = Volatile.Read(ref remaining);
            if (current <= 0)
                return false;
            if (Interlocked.CompareExchange(ref remaining, current - 1, current) == current)
                return true;
        }
    }
}

internal sealed class FailureInjectingPasswordResetProcessor(AuthService inner) : IPasswordResetProcessor
{
    public Task<Result> ProcessForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        if (IdentityServerWorkflowFailureInjection.ConsumePasswordResetProcessorFailure())
            return Task.FromResult(Result.Failure("Injected pre-delivery password reset failure.", 503));

        return inner.ProcessForgotPasswordAsync(request, ct);
    }
}

public static class TestData
{
    public static readonly Guid RoleTypeId = XFramework.TestInfrastructure.TestConstants.RoleTypeId;
}

internal sealed class TestStorageServiceWrapper(
    DbContext db,
    IServiceScopeFactory scopeFactory) : IStorageServiceWrapper
{
    private readonly Dictionary<Guid, StorageUploadSessionResponse> _sessions = new();

    public IStorageFileCrudService StorageFile { get; init; } = null!;
    public IStorageFileTypeCrudService StorageFileType { get; init; } = null!;

    public Task<byte[]> ExecuteQueryAsync(byte[] queryDescriptorBytes, CancellationToken ct = default) =>
        throw new NotSupportedException("Storage queries are not used by these tests.");

    public Task<byte[]> ExecuteChangesAsync(byte[] saveChangesRequestBytes, CancellationToken ct = default) =>
        throw new NotSupportedException("Storage changes are not used by these tests.");

    public IAsyncEnumerable<byte[]> ExecuteQueryStreamAsync(byte[] queryDescriptorBytes, CancellationToken ct = default) =>
        throw new NotSupportedException("Storage query streams are not used by these tests.");

    public async Task<QueryResponse<StorageUploadMetadataResponse>> EnsureStorageUploadMetadata(
        EnsureStorageUploadMetadataRequest request,
        CancellationToken ct = default)
    {
        var tenantId = request.Metadata.TenantId ?? IntegrationTestFixture.TestTenantId;
        var type = await db.Set<StorageFileType>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == request.ContentType, ct);
        if (type is null)
        {
            type = new StorageFileType
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = request.ContentType,
                SystemReferenceId = Guid.NewGuid(), IsEnabled = true, CreatedAt = DateTime.UtcNow
            };
            db.Add(type);
        }

        var group = await db.Set<StorageFileIdentifierGroup>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == request.IdentifierGroupName, ct);
        if (group is null)
        {
            group = new StorageFileIdentifierGroup
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = request.IdentifierGroupName,
                SystemReferenceId = Guid.NewGuid(), IsEnabled = true, CreatedAt = DateTime.UtcNow
            };
            db.Add(group);
        }

        var identifier = await db.Set<StorageFileIdentifier>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == request.IdentifierName, ct);
        if (identifier is null)
        {
            identifier = new StorageFileIdentifier
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = request.IdentifierName,
                Description = request.IdentifierDescription, GroupId = group.Id,
                IsEnabled = true, CreatedAt = DateTime.UtcNow
            };
            db.Add(identifier);
        }

        await db.SaveChangesAsync(ct);
        return new QueryResponse<StorageUploadMetadataResponse>
        {
            HttpStatusCode = HttpStatusCode.OK,
            Response = new StorageUploadMetadataResponse
            {
                TypeId = type.Id,
                StorageFileIdentifierId = identifier.Id
            }
        };
    }

    public async Task<QueryResponse<StorageUploadSessionResponse>> CreateStorageUploadSession(
        CreateStorageUploadSessionRequest request,
        CancellationToken ct = default)
    {
        var tenantId = request.Metadata.TenantId ?? IntegrationTestFixture.TestTenantId;
        var storageFileId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var objectKey = $"{tenantId:N}/{storageFileId:N}/{request.FileName}";
        var bucketName = $"xframework-test-{tenantId:N}";
        var publicUrl = $"https://storage.test/{objectKey}";
        var now = DateTime.UtcNow;

        var file = new StorageFile
        {
            Id = storageFileId,
            TenantId = tenantId,
            Name = request.FileName,
            ContentType = request.ContentType,
            TypeId = request.TypeId,
            Identifier = request.Identifier == Guid.Empty ? storageFileId : request.Identifier,
            StorageFileIdentifierId = request.StorageFileIdentifierId,
            FileSize = request.TotalSizeBytes,
            ContentLengthBytes = request.TotalSizeBytes,
            Hash = request.ExpectedSha256Hash,
            Sha256Hash = request.ExpectedSha256Hash,
            ContentPath = objectKey,
            BlobContainer = bucketName,
            BucketName = bucketName,
            ObjectKey = objectKey,
            PublicUrl = publicUrl,
            Status = StorageFileStatus.Pending,
            Visibility = request.Visibility,
            UnclaimedUntil = request.RequireClaim ? now : null,
            UploadStartedAt = now,
            CreatedAt = now,
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        };

        db.Set<StorageFile>().Add(file);
        await db.SaveChangesAsync(ct);
        var fileResponse = ToFileResponse(file);
        db.Entry(file).State = EntityState.Detached;

        var response = new StorageUploadSessionResponse
        {
            Id = sessionId,
            TenantId = tenantId,
            StorageFileId = storageFileId,
            UploadId = sessionId.ToString("N"),
            Status = StorageUploadSessionStatus.Created,
            ChunkSizeBytes = request.ChunkSizeBytes ?? (int)request.TotalSizeBytes,
            TotalSizeBytes = request.TotalSizeBytes,
            TotalParts = 1,
            UploadedParts = 0,
            ExpectedSha256Hash = request.ExpectedSha256Hash,
            ExpiresAt = now.AddMinutes(30),
            File = fileResponse
        };

        _sessions[sessionId] = response;

        return new QueryResponse<StorageUploadSessionResponse>
        {
            HttpStatusCode = HttpStatusCode.Created,
            Response = response
        };
    }

    public async Task<QueryResponse<StorageUploadPartResponse>> UploadStorageFilePart(
        UploadStorageFilePartRequest request,
        CancellationToken ct = default)
    {
        if (IdentityServerWorkflowFailureInjection.StorageFailurePoint == TestStorageFailurePoint.CancelUploadPart)
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);

        if (IdentityServerWorkflowFailureInjection.StorageFailurePoint == TestStorageFailurePoint.UploadPart)
        {
            return new QueryResponse<StorageUploadPartResponse>
            {
                HttpStatusCode = HttpStatusCode.BadGateway,
                Message = "Injected upload part failure"
            };
        }

        return new QueryResponse<StorageUploadPartResponse>
        {
            HttpStatusCode = HttpStatusCode.Created,
            Response = new StorageUploadPartResponse
            {
                Id = Guid.NewGuid(),
                UploadSessionId = request.UploadSessionId,
                PartNumber = request.PartNumber,
                OffsetBytes = request.OffsetBytes,
                SizeBytes = request.ChunkBytes.Length,
                Sha256Hash = request.PartSha256Hash ?? string.Empty,
                ProviderPartId = Guid.NewGuid().ToString("N"),
                UploadedAt = DateTime.UtcNow
            }
        };
    }

    public Task<QueryResponse<StorageUploadPartListResponse>> ListStorageUploadParts(
        ListStorageUploadPartsRequest request,
        CancellationToken ct = default) =>
        throw new NotSupportedException("Storage upload part listing is not used by these tests.");

    public async Task<QueryResponse<StorageFileResponse>> CompleteStorageUploadSession(
        CompleteStorageUploadSessionRequest request,
        CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(request.UploadSessionId, out var session))
        {
            return new QueryResponse<StorageFileResponse>
            {
                HttpStatusCode = HttpStatusCode.NotFound,
                Message = "Upload session not found"
            };
        }

        if (IdentityServerWorkflowFailureInjection.StorageFailurePoint == TestStorageFailurePoint.CompleteUpload)
        {
            return new QueryResponse<StorageFileResponse>
            {
                HttpStatusCode = HttpStatusCode.BadGateway,
                Message = "Injected upload completion failure"
            };
        }

        var file = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .FirstAsync(item => item.Id == session.StorageFileId, ct);
        file.Status = StorageFileStatus.Available;
        file.UploadedAt = DateTime.UtcNow;
        file.CompletedAt = file.UploadedAt;
        file.Sha256Hash = request.ExpectedSha256Hash ?? file.Sha256Hash;
        file.Hash = file.Sha256Hash;
        if (file.UnclaimedUntil is not null)
            file.UnclaimedUntil = DateTime.UtcNow.AddHours(24);
        db.Update(file);
        await db.SaveChangesAsync(ct);
        var response = ToFileResponse(file);
        db.Entry(file).State = EntityState.Detached;

        if (IdentityServerWorkflowFailureInjection.StorageFailurePoint ==
            TestStorageFailurePoint.CredentialAvatarPersistence)
        {
            await db.Set<Contracts.IdentityCredential>()
                .IgnoreQueryFilters()
                .Where(credential => credential.Id == file.Identifier)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        credential => credential.ConcurrencyStamp,
                        Guid.NewGuid()),
                    ct);
        }

        return new QueryResponse<StorageFileResponse>
        {
            HttpStatusCode = HttpStatusCode.OK,
            Response = response
        };
    }

    public async Task<QueryResponse<StorageFileResponse>> ClaimStorageFile(
        ClaimStorageFileRequest request,
        CancellationToken ct = default)
    {
        IdentityServerWorkflowFailureInjection.RecordStorageClaim();
        if (IdentityServerWorkflowFailureInjection.ConsumeStorageClaimFailure())
        {
            return new QueryResponse<StorageFileResponse>
            {
                HttpStatusCode = HttpStatusCode.BadGateway,
                Message = "Injected storage claim rejection"
            };
        }

        await using var claimScope = scopeFactory.CreateAsyncScope();
        var claimDb = claimScope.ServiceProvider.GetRequiredService<DbContext>();
        var tenantId = request.Metadata.TenantId ?? IntegrationTestFixture.TestTenantId;
        var now = DateTime.UtcNow;
        var updated = await claimDb.Set<StorageFile>()
            .IgnoreQueryFilters()
            .Where(item =>
                item.Id == request.StorageFileId &&
                item.TenantId == tenantId &&
                !item.IsDeleted &&
                item.Status == StorageFileStatus.Available)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.UnclaimedUntil, (DateTime?)null)
                .SetProperty(item => item.ModifiedAt, now)
                .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);
        if (updated == 0)
            return new QueryResponse<StorageFileResponse> { HttpStatusCode = HttpStatusCode.NotFound };

        var file = await claimDb.Set<StorageFile>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == request.StorageFileId, ct);
        var response = ToFileResponse(file);
        return new QueryResponse<StorageFileResponse>
        {
            HttpStatusCode = HttpStatusCode.OK,
            Response = response
        };
    }

    public async Task<CmdResponse> AbortStorageUploadSession(
        AbortStorageUploadSessionRequest request,
        CancellationToken ct = default)
    {
        IdentityServerWorkflowFailureInjection.RecordAbort();

        if (_sessions.Remove(request.UploadSessionId, out var session))
        {
            await db.Set<StorageFile>()
                .IgnoreQueryFilters()
                .Where(file => file.Id == session.StorageFileId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(file => file.Status, StorageFileStatus.Failed)
                        .SetProperty(file => file.ModifiedAt, DateTime.UtcNow),
                    ct);
        }

        return new CmdResponse { HttpStatusCode = HttpStatusCode.OK };
    }

    public async Task<QueryResponse<StorageFileResponse>> GetStorageFile(
        GetStorageFileRequest request,
        CancellationToken ct = default)
    {
        var tenantId = request.Metadata.TenantId ?? IntegrationTestFixture.TestTenantId;
        var file = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(item => item.StorageFileIdentifier)
            .ThenInclude(identifier => identifier!.Group)
            .FirstOrDefaultAsync(x => x.Id == request.StorageFileId && x.TenantId == tenantId, ct);
        return file is null
            ? new QueryResponse<StorageFileResponse> { HttpStatusCode = HttpStatusCode.NotFound }
            : new QueryResponse<StorageFileResponse>
            {
                HttpStatusCode = HttpStatusCode.OK,
                Response = ToFileResponse(file)
            };
    }

    public Task<QueryResponse<StorageFileListResponse>> GetStorageFiles(
        GetStorageFilesRequest request,
        CancellationToken ct = default) =>
        throw new NotSupportedException("Storage file listing is not used by these tests.");

    public Task<QueryResponse<StorageDownloadUrlResponse>> GetStorageDownloadUrl(
        GetStorageDownloadUrlRequest request,
        CancellationToken ct = default) =>
        throw new NotSupportedException("Storage download URLs are not used by these tests.");

    public Task<QueryResponse<StoragePublicUrlResponse>> GetStoragePublicUrl(
        GetStoragePublicUrlRequest request,
        CancellationToken ct = default) =>
        throw new NotSupportedException("Storage public URLs are not used by these tests.");

    public async Task<CmdResponse> DeleteStorageFile(
        DeleteStorageFileRequest request,
        CancellationToken ct = default)
    {
        IdentityServerWorkflowFailureInjection.RecordDelete();
        if (IdentityServerWorkflowFailureInjection.FailNextStorageDelete)
        {
            IdentityServerWorkflowFailureInjection.FailNextStorageDelete = false;
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadGateway };
        }

        var now = DateTime.UtcNow;
        var affected = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .Where(item => item.Id == request.StorageFileId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Status, StorageFileStatus.Deleted)
                    .SetProperty(item => item.IsDeleted, true)
                    .SetProperty(item => item.DeletedAt, now)
                    .SetProperty(item => item.ModifiedAt, now),
                ct);

        return new CmdResponse
        {
            HttpStatusCode = affected == 0 ? HttpStatusCode.NotFound : HttpStatusCode.OK
        };
    }

    public Task<QueryResponse<StorageFileResponse>> RestoreStorageFile(
        RestoreStorageFileRequest request,
        CancellationToken ct = default) =>
        throw new NotSupportedException("Storage restores are not used by these tests.");

    public Task<QueryResponse<StorageRetentionCleanupResponse>> CleanupStorageRetention(
        CleanupStorageRetentionRequest request,
        CancellationToken ct = default) =>
        throw new NotSupportedException("Storage cleanup is not used by these tests.");

    public Task<QueryResponse<StorageFileValidationResponse>> ValidateStorageFileReference(
        ValidateStorageFileReferenceRequest request,
        CancellationToken ct = default) =>
        throw new NotSupportedException("Storage validation is not used by these tests.");

    private static StorageFileResponse ToFileResponse(StorageFile file) => new()
    {
        Id = file.Id,
        TenantId = file.TenantId,
        Name = file.Name ?? string.Empty,
        ContentType = file.ContentType,
        TypeId = file.TypeId,
        Identifier = file.Identifier,
        StorageFileIdentifierId = file.StorageFileIdentifierId,
        Status = file.Status,
        Visibility = file.Visibility,
        BucketName = file.BucketName,
        ObjectKey = file.ObjectKey,
        ContentLengthBytes = file.ContentLengthBytes,
        Sha256Hash = file.Sha256Hash,
        PublicUrl = file.PublicUrl,
        CdnBaseUrl = file.CdnBaseUrl,
        BlobContainer = file.BlobContainer,
        StorageFileIdentifierName = file.StorageFileIdentifier?.Name,
        StorageFileIdentifierGroupName = file.StorageFileIdentifier?.Group?.Name,
        UploadStartedAt = file.UploadStartedAt,
        CompletedAt = file.CompletedAt,
        UnclaimedUntil = file.UnclaimedUntil,
        CreatedAt = file.CreatedAt
    };
}
