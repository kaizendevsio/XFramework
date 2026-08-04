using System.Runtime.CompilerServices;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bolt.Client;
using Bolt.Hub.Extensions;
using FluentValidation;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
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
using XFramework.Domain.Shared.Extensions;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Extensions;
using XFramework.Integration.Extensions;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;

namespace Storage.IntegrationTests;

[SetUpFixture]
public sealed class StorageIntegrationTestFixture
{
    private const string ExternalConnectionStringEnvironmentVariable = "STORAGE_TEST_POSTGRES_CONNECTION";
    private const string StorageServiceGenerationId = "storage-test-g1";
    private const string StorageServiceSecret = "storage-integration-service-secret-2026";
    private const string TestClientGenerationId = "storage-client-test-g1";
    private const string TestClientSecret = "storage-integration-client-secret-2026";
    private static readonly Guid TestIdentityId = Guid.Parse("74c5c42e-8035-454f-a3ba-da3844759c6f");
    public static readonly Guid TestCredentialId = Guid.Parse("ef70fc0d-eed3-4b97-a111-808b31e0cd4c");
    private static readonly TestInvocationIdentityOptions InvocationIdentity = new(
        "storage-test-actor-token",
        "storage-test-service-token",
        XFrameworkServiceNames.Portal,
        TestConstants.TenantId,
        TestCredentialId,
        TestIdentityId,
        Guid.Parse("00000000-0000-0000-0000-000000000833"));

    private static PostgreSqlContainer postgres = null!;
    private static bool ownsPostgresContainer;
    private static WebApplication boltApp = null!;
    private static WebApplication storageApp = null!;
    private static WebApplication testClientApp = null!;
    private static IServiceScope testClientScope = null!;
    private static Task? boltTask;
    private static Task? storageTask;
    private static Task? testClientTask;
    private static string transportSigningKeyPath = null!;
    private static string userJwtPublicKeyPath = null!;
    private static RSA serviceSigningKey = null!;
    private static string serviceSigningKeyId = null!;

    public static string ConnectionString { get; private set; } = null!;
    public static string BoltUrl => TestConstants.Ports.StorageBolt;
    public static string StorageUrl => TestConstants.Ports.StorageServer;
    public static string TestClientUrl => TestConstants.Ports.StorageTestClient;
    public static Guid TestTenantId => TestConstants.TenantId;
    public static IntegrationStorageObjectProvider Provider { get; } = new();

    public static IStorageServiceWrapper ServiceWrapper =>
        testClientScope.ServiceProvider.GetRequiredService<IStorageServiceWrapper>();

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
        transportSigningKeyPath = Path.Combine(
            Path.GetTempPath(),
            $"xframework-storage-tests-{Guid.NewGuid():N}",
            "bolt-transport-signing-key.pem");
        userJwtPublicKeyPath = Path.Combine(
            Path.GetDirectoryName(transportSigningKeyPath)!,
            "user-jwt-public-key.pem");
        Directory.CreateDirectory(Path.GetDirectoryName(userJwtPublicKeyPath)!);
        using (var userJwtKey = RSA.Create(3072))
            await File.WriteAllTextAsync(userJwtPublicKeyPath, userJwtKey.ExportSubjectPublicKeyInfoPem());

        boltApp = StartBolt();
        await TestHostWaiter.WaitForHealth($"{BoltUrl}/health/live", boltTask);

        storageApp = StartStorage();
        await TestHostWaiter.WaitForHealth($"{StorageUrl}/health/live", storageTask);

        testClientApp = StartTestClient();
        await TestHostWaiter.WaitForHealth($"{TestClientUrl}/health/live", testClientTask);
        testClientScope = testClientApp.Services.CreateScope();

        await WaitForBoltClients();
    }

    [OneTimeTearDown]
    public async Task GlobalTeardown()
    {
        try { testClientScope?.Dispose(); } catch { }
        await StopApplicationAsync(testClientApp);
        await StopApplicationAsync(storageApp);
        await StopApplicationAsync(boltApp);
        serviceSigningKey?.Dispose();

        var signingKeyDirectory = Path.GetDirectoryName(transportSigningKeyPath);
        if (!string.IsNullOrWhiteSpace(signingKeyDirectory) && Directory.Exists(signingKeyDirectory))
            Directory.Delete(signingKeyDirectory, recursive: true);

        if (ownsPostgresContainer && postgres is not null)
        {
            try
            {
                await postgres.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));
            }
            catch (TimeoutException)
            {
                TestContext.Progress.WriteLine("Storage test PostgreSQL cleanup exceeded 15 seconds.");
            }
        }
    }

    private static async Task StopApplicationAsync(WebApplication? app)
    {
        if (app is null)
            return;

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await app.StopAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            TestContext.Progress.WriteLine("Storage test host shutdown exceeded 5 seconds.");
        }
        catch (Exception ex)
        {
            TestContext.Progress.WriteLine($"Storage test host shutdown failed: {ex.GetType().Name}");
        }
    }

    public static AppDbContext CreateDbContext()
    {
        ForceModelAssemblies();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenant:DefaultId"] = TestTenantId.ToString()
            })
            .Build();

        return new AppDbContext(
            options,
            new HttpContextAccessor(),
            configuration,
            new XFramework.TestInfrastructure.TestEffectiveTenantContextAccessor(TestTenantId));
    }

    private static WebApplication StartBolt()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseUrls(BoltUrl);
        OverrideConfig(builder, "Bolt.StorageTest", BoltUrl);
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
        MapTestTokenAuthority(app, builder.Configuration);
        app.UseCorrelationId();
        app.UseAppServices();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        StartApplication(app);
        boltTask = app.WaitForShutdownAsync();
        return app;
    }

    private static WebApplication StartStorage()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(StorageUrl);
        OverrideConfig(
            builder,
            XFrameworkServiceNames.Storage,
            StorageUrl);
        builder.Configuration["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/bolt/ws";

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<AuditInterceptor>();
        builder.Services.AddDbContext<DbContext, AppDbContext>((sp, options) => options
            .UseNpgsql(ConnectionString,
                npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .ConfigureWarnings(w => w.Ignore(
                RelationalEventId.BoolWithDefaultWarning))
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
        builder.Services.AddTestInvocationServer(InvocationIdentity);
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

        StartApplication(app);
        storageTask = app.WaitForShutdownAsync();
        return app;
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
            ["BoltConfiguration:GenerateServiceAccessToken"] = "false",
            ["ServiceIdentity:ClientId"] = XFrameworkServiceNames.Portal,
            ["ServiceIdentity:Authority"] = BoltUrl,
            ["ServiceIdentity:AllowInsecureHttp"] = "true",
            ["ServiceIdentity:GenerationId"] = TestClientGenerationId,
            ["ServiceIdentity:ClientSecret"] = TestClientSecret,
            ["ServiceIdentity:DefaultScopes:0"] = XFrameworkServiceScopes.StorageRead,
            ["ServiceIdentity:DefaultScopes:1"] = XFrameworkServiceScopes.StorageWrite,
            ["ServiceIdentity:DefaultScopes:2"] = XFrameworkServiceScopes.BoltService,
            ["ServiceIdentity:DefaultScopes:3"] = XFrameworkServiceScopes.DataContextQuery,
            ["ServiceIdentity:DefaultScopes:4"] = XFrameworkServiceScopes.DataContextMutate,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Kestrel:Endpoints:Http:Url"] = TestClientUrl,
            ["urls"] = TestClientUrl,
            ["Logging:LogLevel:Default"] = "Warning"
        });

        builder.Services.InstallStandardServices<StorageIntegrationTestFixture>(builder.Configuration);
        builder.Services.AddSingleton(new DeviceAgentProvider("StorageTest"));
        builder.Services.AddXFrameworkBoltClient(builder.Configuration, autoConnect: false);
        builder.Services.AddTestInvocationClient(InvocationIdentity);
        builder.Services.AddStorageWrapperServices();
        builder.Services.AddRemoteDataContext();
        builder.Services.AddScoped(_ => new RequestMetadata
        {
            RequestedTenantId = TestTenantId,
            RequestId = Guid.NewGuid()
        });

        var app = builder.Build();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        StartApplication(app);
        testClientTask = app.WaitForShutdownAsync();
        return app;
    }

    private static void StartApplication(WebApplication app)
    {
        app.StartAsync()
            .WaitAsync(TimeSpan.FromSeconds(30))
            .GetAwaiter()
            .GetResult();
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

    private static void OverrideConfig(
        WebApplicationBuilder builder,
        string clientName,
        string serverUrl)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultDatabaseConnection"] = ConnectionString,
            ["DefaultDatabaseConnection"] = ConnectionString,
            ["BoltConfiguration:ClientGuid"] = Guid.NewGuid().ToString(),
            ["BoltConfiguration:ClientName"] = clientName,
            ["BoltConfiguration:GenerateServiceAccessToken"] = "false",
            ["ServiceIdentity:ClientId"] = clientName,
            ["ServiceIdentity:Authority"] = BoltUrl,
            ["ServiceIdentity:AllowInsecureHttp"] = "true",
            ["ServiceIdentity:GenerationId"] = StorageServiceGenerationId,
            ["ServiceIdentity:ClientSecret"] = StorageServiceSecret,
            ["ServiceIdentity:DefaultScopes:0"] = XFrameworkServiceScopes.StorageRead,
            ["ServiceIdentity:DefaultScopes:1"] = XFrameworkServiceScopes.StorageWrite,
            ["ServiceIdentity:DefaultScopes:2"] = XFrameworkServiceScopes.BoltService,
            ["ServiceIdentity:BoltTransportTokenIssuer:Enabled"] = "true",
            ["ServiceIdentity:BoltTransportTokenIssuer:SigningKeyPath"] = transportSigningKeyPath,
            ["ServiceIdentity:Clients:0:ClientId"] = XFrameworkServiceNames.Storage,
            ["ServiceIdentity:Clients:0:GenerationId"] = StorageServiceGenerationId,
            ["ServiceIdentity:Clients:0:ClientSecret"] = StorageServiceSecret,
            ["ServiceIdentity:Clients:0:AllowedAudiences"] = string.Join(',',
                XFrameworkServiceNames.Storage,
                XFrameworkServiceNames.BoltHub),
            ["ServiceIdentity:Clients:0:AllowedScopes"] = string.Join(',',
                XFrameworkServiceScopes.BoltService,
                XFrameworkServiceScopes.StorageRead,
                XFrameworkServiceScopes.StorageWrite),
            ["ServiceIdentity:Clients:1:ClientId"] = XFrameworkServiceNames.Portal,
            ["ServiceIdentity:Clients:1:GenerationId"] = TestClientGenerationId,
            ["ServiceIdentity:Clients:1:ClientSecret"] = TestClientSecret,
            ["ServiceIdentity:Clients:1:AllowedAudiences"] = XFrameworkServiceNames.Storage,
            ["ServiceIdentity:Clients:1:AllowedScopes"] = string.Join(',',
                XFrameworkServiceScopes.BoltService,
                XFrameworkServiceScopes.StorageRead,
                XFrameworkServiceScopes.StorageWrite,
                XFrameworkServiceScopes.DataContextQuery,
                XFrameworkServiceScopes.DataContextMutate),
            ["BoltTransportAuthentication:MetadataAddress"] = $"{BoltUrl}{BoltTransportTokenConstants.MetadataPath}",
            ["BoltTransportAuthentication:Issuer"] = XFrameworkServiceNames.IdentityServer,
            ["BoltTransportAuthentication:Audience"] = XFrameworkServiceNames.BoltHub,
            ["BoltTransportAuthentication:RequireHttpsMetadata"] = "false",
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Kestrel:Endpoints:Http:Url"] = serverUrl,
            ["urls"] = serverUrl,
            ["JwtOptions:ValidAudience"] = "http://localhost:18301",
            ["JwtOptions:ValidIssuer"] = "http://localhost:18301",
            ["JwtOptions:GenerationId"] = "storage-test-jwt-g1",
            ["JwtOptions:SigningPublicKeyPath"] = userJwtPublicKeyPath,
            ["JwtOptions:SigningPrivateKeyPath"] = string.Empty,
            ["JwtOptions:AccessTokenLifespan"] = "00:30:00",
            ["JwtOptions:RefreshTokenLifespan"] = "00:30:00",
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

    private static void MapTestTokenAuthority(WebApplication app, IConfiguration configuration)
    {
        var serviceIdentity = ServiceIdentityConfiguration.FromConfiguration(
            configuration,
            TimeProvider.System.GetUtcNow(),
            "Test");
        var signer = new FileBackedBoltTransportTokenSigner(serviceIdentity);
        serviceSigningKey = RSA.Create(3072);
        serviceSigningKeyId = $"service-{Base64UrlEncoder.Encode(SHA256.HashData(serviceSigningKey.ExportSubjectPublicKeyInfo()))}";

        app.MapGet(BoltTransportTokenConstants.MetadataPath, () => Results.Json(new
        {
            issuer = XFrameworkServiceNames.IdentityServer,
            jwks_uri = $"{BoltUrl}{BoltTransportTokenConstants.JsonWebKeySetPath}",
            token_endpoint = $"{BoltUrl}{BoltTransportTokenConstants.TokenEndpointPath}",
            id_token_signing_alg_values_supported = new[] { BoltTransportTokenConstants.Algorithm }
        }));
        app.MapGet(
            BoltTransportTokenConstants.JsonWebKeySetPath,
            () => Results.Json(signer.GetJsonWebKeySet()));
        app.MapPost(
            BoltTransportTokenConstants.TokenEndpointPath,
            (TestBoltTransportTokenRequest request) => IssueTestBoltTransportToken(request, signer));
        app.MapPost(
            "/api/service-identity/token",
            (IssueServiceTokenRequest request) => IssueTestServiceToken(request));
        app.MapPost(
            "/api/service-identity/signing-keys/query",
            () => Results.Json(new ServiceSigningKeysResponse
            {
                Keys =
                [
                    new ServiceSigningKeyResponse
                    {
                        KeyId = serviceSigningKeyId,
                        Algorithm = SecurityAlgorithms.RsaSha256,
                        PublicKeyPem = serviceSigningKey.ExportSubjectPublicKeyInfoPem(),
                        CreatedAtUtc = DateTime.UtcNow,
                        ActivatedAtUtc = DateTime.UtcNow,
                        IsActive = true
                    }
                ]
            }));
    }

    private static IResult IssueTestBoltTransportToken(
        TestBoltTransportTokenRequest request,
        IBoltTransportTokenSigner signer)
    {
        var generationId = request.ClientId switch
        {
            XFrameworkServiceNames.Storage when SecretsMatch(request.ClientSecret, StorageServiceSecret) => StorageServiceGenerationId,
            XFrameworkServiceNames.Portal when SecretsMatch(request.ClientSecret, TestClientSecret) => TestClientGenerationId,
            _ => null
        };
        if (generationId is null)
            return Results.Unauthorized();

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var expiresAt = issuedAt.AddSeconds(ServiceIdentityConfiguration.DefaultBoltTransportTokenLifetimeSeconds);
        return Results.Json(new ServiceTokenResponse
        {
            AccessToken = signer.Sign(request.ClientId, generationId, issuedAt, expiresAt),
            ExpiresAtUtc = expiresAt.UtcDateTime,
            TokenType = "Bearer"
        });
    }

    private static IResult IssueTestServiceToken(IssueServiceTokenRequest request)
    {
        var generationId = ResolveCredentialGeneration(request.ClientId, request.ClientSecret);
        if (generationId is null)
            return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Audience))
            return Results.BadRequest();

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).UtcDateTime;
        var expiresAt = issuedAt.AddMinutes(5);
        List<Claim> claims =
        [
            new("client_id", request.ClientId),
            new("client_credential_generation", generationId),
            new(JwtRegisteredClaimNames.Sub, request.ClientId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(
                JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(issuedAt).ToString(),
                ClaimValueTypes.Integer64)
        ];
        if (request.Scopes.Count > 0)
            claims.Add(new Claim("scope", string.Join(' ', request.Scopes)));

        var token = new JwtSecurityToken(
            issuer: XFrameworkServiceNames.IdentityServer,
            audience: request.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(
                new RsaSecurityKey(serviceSigningKey) { KeyId = serviceSigningKeyId },
                SecurityAlgorithms.RsaSha256));

        return Results.Json(new ServiceTokenResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAtUtc = expiresAt,
            TokenType = "Bearer"
        });
    }

    private static string? ResolveCredentialGeneration(string clientId, string? clientSecret) => clientId switch
    {
        XFrameworkServiceNames.Storage when SecretsMatch(clientSecret, StorageServiceSecret) => StorageServiceGenerationId,
        XFrameworkServiceNames.Portal when SecretsMatch(clientSecret, TestClientSecret) => TestClientGenerationId,
        _ => null
    };

    private static bool SecretsMatch(string? supplied, string expected)
    {
        if (supplied is null)
            return false;

        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        try
        {
            return suppliedBytes.Length == expectedBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(suppliedBytes);
            CryptographicOperations.ZeroMemory(expectedBytes);
        }
    }

    private sealed record TestBoltTransportTokenRequest(string ClientId, string ClientSecret);

    private static async Task MigrateAndSeed()
    {
        ForceModelAssemblies();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new AppDbContext(
            options,
            new HttpContextAccessor(),
            new ConfigurationBuilder().Build(),
            new XFramework.TestInfrastructure.TestEffectiveTenantContextAccessor(TestTenantId));
        await db.Database.MigrateAsync();
        await TestSeedData.SeedAll(db);
        await SeedStorageReferenceData(db);
        await db.SaveChangesAsync();
    }

    private static async Task SeedStorageReferenceData(AppDbContext db)
    {
        if (!await db.Set<IdentityInformation>().IgnoreQueryFilters().AnyAsync(item => item.Id == TestIdentityId))
        {
            db.Set<IdentityInformation>().Add(new IdentityInformation
            {
                Id = TestIdentityId,
                TenantId = TestTenantId,
                IdentityName = "Storage Integration User",
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true,
                ConcurrencyStamp = Guid.NewGuid()
            });
        }

        if (!await db.Set<IdentityCredential>().IgnoreQueryFilters().AnyAsync(item => item.Id == TestCredentialId))
        {
            db.Set<IdentityCredential>().Add(new IdentityCredential
            {
                Id = TestCredentialId,
                TenantId = TestTenantId,
                IdentityInfoId = TestIdentityId,
                UserName = "storage-integration-user",
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true,
                ConcurrencyStamp = Guid.NewGuid()
            });
        }

        if (!await db.Set<IdentityRole>().IgnoreQueryFilters().AnyAsync(item =>
                item.CredentialId == TestCredentialId && item.TypeId == TestConstants.RoleTypeId))
        {
            db.Set<IdentityRole>().Add(new IdentityRole
            {
                Id = Guid.NewGuid(),
                TenantId = TestTenantId,
                CredentialId = TestCredentialId,
                TypeId = TestConstants.RoleTypeId,
                RoleExpiration = DateTime.UtcNow.AddYears(1),
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true,
                ConcurrencyStamp = Guid.NewGuid()
            });
        }

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
        TestDatabaseModel.LoadMigrationAssemblies();
    }
}
