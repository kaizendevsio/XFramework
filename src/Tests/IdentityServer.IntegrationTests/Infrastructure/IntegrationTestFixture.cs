using Bolt.Client;
using FluentValidation;
using IdentityServer.Domain.Shared;
using IdentityServer.Api.Features.Verification.Confirm;
using IdentityServer.Api.Generated;
using IdentityServer.Api.Services;
using IdentityServer.Integration.Drivers;
using Communications.Integration.Drivers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Text.Json.Serialization;
using Bolt.Hub.Extensions;
using Storage.Domain.Shared.Contracts.Requests;
using Storage.Domain.Shared.Contracts.Responses;
using Storage.Integration.Drivers;
using Testcontainers.PostgreSql;
using XFramework.Core.DataContext;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Extensions;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Extensions;
using XFramework.Integration.Extensions;
using XFramework.Integration.Security;
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
    public static string BoltUrl => XFramework.TestInfrastructure.TestConstants.Ports.IdentityBolt;
    public static string IdentityServerUrl => XFramework.TestInfrastructure.TestConstants.Ports.IdentityServer;
    public static string TestClientUrl => XFramework.TestInfrastructure.TestConstants.Ports.IdentityTestClient;

    public static IServiceProvider Services => _identityServerApp.Services;
    private const string TestServiceClientId = "TestClient";
    private const string TestServiceClientSecret = "IdentityServerIntegrationTestSecret-2026";

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
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseUrls(BoltUrl);
        OverrideConfiguration(builder, "Bolt.Test", "00000000-0000-0000-0000-000000000001", BoltUrl);
        builder.Services.InstallServicesInAssembly<Bolt.Hub.Installers.BoltInstaller>(builder.Configuration, builder.Environment);
        builder.Services.InstallSwagger(builder.Configuration);
        builder.Services.InstallOData(builder.Configuration);
        builder.Services.InstallJwt(builder.Configuration);
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
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(IdentityServerUrl);

        // Override configuration first — installers read config at registration time
        OverrideConfiguration(builder, XFrameworkServiceNames.IdentityServer, Guid.NewGuid().ToString(), IdentityServerUrl);
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
        builder.Services.AddTenantModuleFeatures();
        builder.Services.AddCommunicationsWrapperServices();
        builder.Services.AddScoped<IStorageServiceWrapper, TestStorageServiceWrapper>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IServiceIdentityService, ServiceIdentityService>();
        builder.Services.AddSingleton<IIdentitySigningKeyProvider, IdentityServerLocalSigningKeyProvider>();
        builder.Services.AddValidatorsFromAssemblyContaining<AuthService>();
        builder.Services.AddXFrameworkBoltClient(builder.Configuration, autoConnect: false);

        // Register DataContext handler so IdentityServer can serve __db_query__/__db_changes__ via Bolt
        builder.Services.AddDataContextHandler(typeof(AuthService).Assembly);

        var app = (WebApplication)builder.Build();
        RegisterIdentityServerBoltHandlers(app);
        app.UseCorrelationId();
        app.MapGeneratedEndpoints();
        app.MapConfirmVerificationEndpoint();
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
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(TestClientUrl);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BoltConfiguration:ClientName"] = "TestClient",
            ["BoltConfiguration:ClientGuid"] = Guid.NewGuid().ToString(),
            ["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/bolt/ws",
            ["ServiceIdentity:ClientId"] = TestServiceClientId,
            ["ServiceIdentity:ClientSecret"] = TestServiceClientSecret,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Kestrel:Endpoints:Http:Url"] = TestClientUrl,
            ["urls"] = TestClientUrl,
            ["JwtOptions:ValidAudience"] = "http://localhost:18261",
            ["JwtOptions:ValidIssuer"] = "http://localhost:18261",
            ["JwtOptions:Secret"] = "Mm1VFHaqZ7MoVJyZd1zrAKxTpsXbYG6RqSMKYG2cV7RBBUdmsm97HOfKyA7MZ1LUl77ZklJPJfnegohyHqJIoQ983fTKmJcY",
            ["JwtOptions:AccessTokenLifespan"] = "00:30:00",
            ["JwtOptions:RefreshTokenLifespan"] = "00:30:00",
            ["Logging:LogLevel:Default"] = "Warning",
        });

        // Core services needed by service wrappers.
        // NOTE(Task13): Test client uses thin-protocol BoltDriver. IdentityServer still uses
        // SignalR for handler registration, so StreamFlow tests will time out until Task 13
        // updates the source generator to emit thin-protocol handlers.
        builder.Services.InstallStandardServices<IntegrationTestFixture>(builder.Configuration);
        builder.Services.AddSingleton(new XFramework.Domain.Shared.BusinessObjects.DeviceAgentProvider("IntegrationTest"));
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
            ["ServiceIdentity:Clients:0:ClientId"] = TestServiceClientId,
            ["ServiceIdentity:Clients:0:ClientSecret"] = TestServiceClientSecret,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Kestrel:Endpoints:Http:Url"] = serverUrl,
            ["urls"] = serverUrl,
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

        if (appTask is { IsCompleted: true })
        {
            throw new InvalidOperationException("Application stopped before the health endpoint became available.");
        }

        throw new TimeoutException($"Service at {url} did not become healthy within {timeoutSeconds}s");
    }
}

public static class TestData
{
    public static readonly Guid RoleTypeId = XFramework.TestInfrastructure.TestConstants.RoleTypeId;
}

internal sealed class TestStorageServiceWrapper(DbContext db) : IStorageServiceWrapper
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

    public async Task<QueryResponse<StorageUploadSessionResponse>> CreateStorageUploadSession(
        CreateStorageUploadSessionRequest request)
    {
        var tenantId = request.Metadata.TenantId ?? IntegrationTestFixture.TestTenantId;
        var storageFileId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var objectKey = $"identity-credential-avatars/credentials/{request.Identifier:N}/{storageFileId:N}";
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
            BlobContainer = CredentialAvatarPolicy.BlobContainer,
            ObjectKey = objectKey,
            PublicUrl = publicUrl,
            Status = StorageFileStatus.Pending,
            Visibility = request.Visibility,
            UploadStartedAt = now,
            CreatedAt = now,
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        };

        db.Set<StorageFile>().Add(file);
        await db.SaveChangesAsync();
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

    public Task<QueryResponse<StorageUploadPartResponse>> UploadStorageFilePart(
        UploadStorageFilePartRequest request)
    {
        return Task.FromResult(new QueryResponse<StorageUploadPartResponse>
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
        });
    }

    public Task<QueryResponse<StorageUploadPartListResponse>> ListStorageUploadParts(ListStorageUploadPartsRequest request) =>
        throw new NotSupportedException("Storage upload part listing is not used by these tests.");

    public async Task<QueryResponse<StorageFileResponse>> CompleteStorageUploadSession(
        CompleteStorageUploadSessionRequest request)
    {
        if (!_sessions.TryGetValue(request.UploadSessionId, out var session))
        {
            return new QueryResponse<StorageFileResponse>
            {
                HttpStatusCode = HttpStatusCode.NotFound,
                Message = "Upload session not found"
            };
        }

        var file = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .FirstAsync(item => item.Id == session.StorageFileId);
        file.Status = StorageFileStatus.Available;
        file.UploadedAt = DateTime.UtcNow;
        file.CompletedAt = file.UploadedAt;
        file.Sha256Hash = request.ExpectedSha256Hash ?? file.Sha256Hash;
        file.Hash = file.Sha256Hash;
        db.Update(file);
        await db.SaveChangesAsync();
        var response = ToFileResponse(file);
        db.Entry(file).State = EntityState.Detached;

        return new QueryResponse<StorageFileResponse>
        {
            HttpStatusCode = HttpStatusCode.OK,
            Response = response
        };
    }

    public Task<CmdResponse> AbortStorageUploadSession(AbortStorageUploadSessionRequest request) =>
        throw new NotSupportedException("Storage upload abort is not used by these tests.");

    public Task<QueryResponse<StorageFileResponse>> GetStorageFile(GetStorageFileRequest request) =>
        throw new NotSupportedException("Storage file reads are not used by these tests.");

    public Task<QueryResponse<StorageFileListResponse>> GetStorageFiles(GetStorageFilesRequest request) =>
        throw new NotSupportedException("Storage file listing is not used by these tests.");

    public Task<QueryResponse<StorageDownloadUrlResponse>> GetStorageDownloadUrl(GetStorageDownloadUrlRequest request) =>
        throw new NotSupportedException("Storage download URLs are not used by these tests.");

    public Task<QueryResponse<StoragePublicUrlResponse>> GetStoragePublicUrl(GetStoragePublicUrlRequest request) =>
        throw new NotSupportedException("Storage public URLs are not used by these tests.");

    public Task<CmdResponse> DeleteStorageFile(DeleteStorageFileRequest request) =>
        throw new NotSupportedException("Storage deletes are not used by these tests.");

    public Task<QueryResponse<StorageFileResponse>> RestoreStorageFile(RestoreStorageFileRequest request) =>
        throw new NotSupportedException("Storage restores are not used by these tests.");

    public Task<QueryResponse<StorageRetentionCleanupResponse>> CleanupStorageRetention(CleanupStorageRetentionRequest request) =>
        throw new NotSupportedException("Storage cleanup is not used by these tests.");

    public Task<QueryResponse<StorageFileValidationResponse>> ValidateStorageFileReference(
        ValidateStorageFileReferenceRequest request) =>
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
        ObjectKey = file.ObjectKey,
        ContentLengthBytes = file.ContentLengthBytes,
        Sha256Hash = file.Sha256Hash,
        PublicUrl = file.PublicUrl,
        CdnBaseUrl = file.CdnBaseUrl,
        UploadStartedAt = file.UploadStartedAt,
        CompletedAt = file.CompletedAt,
        CreatedAt = file.CreatedAt
    };
}
