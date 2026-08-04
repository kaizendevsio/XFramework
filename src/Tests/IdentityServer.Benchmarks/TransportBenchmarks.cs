using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Bolt.Client;
using FluentValidation;
using IdentityServer.Api.Generated;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using MemoryPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Bolt.Protocol;
using Bolt.Hub.Extensions;
using Testcontainers.PostgreSql;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Configurations;
using XFramework.Domain.Shared.Extensions;
using XFramework.Extensions;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Extensions;
using Grpc.Net.Client;
using IdentityServer.Benchmarks.Grpc;
using Contracts = IdentityServer.Domain.Shared.Contracts;

namespace IdentityServer.Benchmarks;

/// <summary>
/// Measures complete XFramework request paths. The paths execute different application
/// stacks, so results are useful for per-path regression tracking, not protocol ranking.
/// Every benchmark validates the returned status before completing.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class TransportBenchmarks
{
    private PostgreSqlContainer _postgres = null!;
    private WebApplication _streamFlowApp = null!;
    private WebApplication _identityServerApp = null!;
    private WebApplication _testClientApp = null!;
    private Task? _streamFlowTask;
    private Task? _identityServerTask;
    private Task? _testClientTask;
    private HttpClient _httpClient = null!;
    private IIdentityServerServiceWrapper _serviceWrapper = null!;
    private IServiceScope _testClientScope = null!;

    // Thin protocol
    private BoltClient _thinServiceClient = null!;
    private BoltClient _thinCallerClient = null!;
    private int _identityServerServiceHash;
    private int _healthCheckCommandHash;

    // gRPC (with hub — fair comparison: Client → Hub → Backend → Hub → Client)
    private WebApplication _grpcBackendApp = null!;
    private WebApplication _grpcHubApp = null!;
    private GrpcChannel _grpcChannel = null!;
    private HealthService.HealthServiceClient _grpcClient = null!;

    private const string BoltUrl = "http://localhost:19000";
    private const string IdentityServerUrl = "http://localhost:19261";
    private const string TestClientUrl = "http://localhost:19262";
    private static string BoltServerUrl => new UriBuilder(BoltUrl)
    {
        Scheme = Uri.UriSchemeWs,
        Path = "bolt/ws"
    }.Uri.ToString();
    private static readonly Guid TestTenantId = Guid.Parse("7602c2d3-01df-4bdb-9a67-02c144e4a2ac");

    private HealthCheckRequest _request = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        // 1. Start Postgres
        _postgres = new PostgreSqlBuilder()
            .WithDatabase("XFramework_Bench")
            .WithUsername("bench_user")
            .WithPassword("bench_password")
            .Build();
        await _postgres.StartAsync();

        // 2. Migrate
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        if (!await db.Set<Contracts.Tenant>().AnyAsync(t => t.Id == TestTenantId))
        {
            db.Set<Contracts.Tenant>().Add(new Contracts.Tenant
            {
                Id = TestTenantId, TenantId = TestTenantId,
                Name = "Bench Tenant", Description = "Benchmark tenant"
            });
            await db.SaveChangesAsync();
        }

        // 3. Start Bolt hub
        _streamFlowApp = StartBolt();
        await WaitForHealth($"{BoltUrl}/health/live");

        // 4. Start IdentityServer
        _identityServerApp = StartIdentityServer();
        await WaitForHealth($"{IdentityServerUrl}/health/live");

        // 5. Seed tenant cache
        var cache = _identityServerApp.Services.GetRequiredService<IMemoryCache>();
        cache.Set($"GetTenant-{TestTenantId}", new Contracts.Tenant
        {
            Id = TestTenantId, TenantId = TestTenantId,
            Name = "Bench Tenant", Description = "Benchmark tenant"
        });

        // 6. Start test client (Bolt consumer)
        _testClientApp = StartTestClient();
        await WaitForHealth($"{TestClientUrl}/health/live");

        // 7. Wait for Bolt clients to connect
        await WaitForBoltClients();

        // 8. Create reusable objects
        _httpClient = new HttpClient { BaseAddress = new Uri(IdentityServerUrl) };
        _testClientScope = _testClientApp.Services.CreateScope();
        _serviceWrapper = _testClientScope.ServiceProvider.GetRequiredService<IIdentityServerServiceWrapper>();
        _request = new HealthCheckRequest
        {
            Metadata = new RequestMetadata
            {
                RequestId = Guid.NewGuid(),
                IpAddress = "127.0.0.1",
                OperationName = "Benchmark",
                DeviceName = "BenchDevice",
                UserAgent = "BenchAgent"
            }
        };

        // Warmup both paths
        for (var i = 0; i < 5; i++)
        {
            await _httpClient.PostAsJsonAsync("/api/health/check", _request);
            await _serviceWrapper.HealthCheck(new HealthCheckRequest
            {
                Metadata = new RequestMetadata
                {
                    RequestId = Guid.NewGuid(),
                    IpAddress = "127.0.0.1", OperationName = "Benchmark",
                    DeviceName = "BenchDevice", UserAgent = "BenchAgent"
                }
            });
        }

        // 10. Setup thin protocol clients
        await SetupThinProtocol();

        // 11. Setup gRPC (benchmark-only)
        await SetupGrpc();

    }

    private async Task SetupGrpc()
    {
        // 1. gRPC Backend (handles actual requests — same role as IdentityServer in Bolt)
        var backendBuilder = WebApplication.CreateBuilder();
        backendBuilder.WebHost.ConfigureKestrel(o =>
        {
            o.ListenLocalhost(19263, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(19264, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        backendBuilder.Services.AddGrpc();
        backendBuilder.Logging.SetMinimumLevel(LogLevel.Error);

        _grpcBackendApp = backendBuilder.Build();
        _grpcBackendApp.MapGrpcService<GrpcHealthBackend>();
        _grpcBackendApp.MapGet("/health/live", () => Results.Ok("healthy"));
        _ = Task.Run(() => _grpcBackendApp.RunAsync());
        await WaitForHealth("http://localhost:19264/health/live");

        // 2. gRPC Hub (proxies to backend — same role as BoltServer in Bolt)
        var backendChannel = GrpcChannel.ForAddress("http://localhost:19263");
        var backendClient = new HealthService.HealthServiceClient(backendChannel);

        var hubBuilder = WebApplication.CreateBuilder();
        hubBuilder.WebHost.ConfigureKestrel(o =>
        {
            o.ListenLocalhost(19266, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(19267, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        hubBuilder.Services.AddGrpc();
        hubBuilder.Services.AddSingleton(backendClient);
        hubBuilder.Logging.SetMinimumLevel(LogLevel.Error);

        _grpcHubApp = hubBuilder.Build();
        _grpcHubApp.MapGrpcService<GrpcHealthHub>();
        _grpcHubApp.MapGet("/health/live", () => Results.Ok("healthy"));
        _ = Task.Run(() => _grpcHubApp.RunAsync());
        await WaitForHealth("http://localhost:19267/health/live");

        // 3. Client connects to the Hub (not the backend) — same as Bolt client → hub
        _grpcChannel = GrpcChannel.ForAddress("http://localhost:19266");
        _grpcClient = new HealthService.HealthServiceClient(_grpcChannel);

        // Warmup
        for (var i = 0; i < 5; i++)
        {
            await _grpcClient.CheckAsync(new HealthCheckReq
            {
                TenantId = TestTenantId.ToString(),
                RequestId = Guid.NewGuid().ToString(),
                IpAddress = "127.0.0.1",
                Name = "Benchmark"
            });
        }
    }

    private async Task SetupThinProtocol()
    {
        var thinServerUri = new Uri($"ws://localhost:19000/bolt/ws");
        var config = new BoltClientOptions { RpcTimeoutSeconds = 30, MinConnections = 1, MaxConnections = 1 };
        var loggerFactory = _streamFlowApp.Services.GetRequiredService<ILoggerFactory>();

        // Compute hashes for routing
        var identityServerServiceId = "3902761a822d4c6b8e2d323fd501bcd6"; // SHA256 of "IdentityServer" — same as SignalR registration
        _identityServerServiceHash = BoltCodec.Fnv1aHash(identityServerServiceId);
        _healthCheckCommandHash = BoltCodec.Fnv1aHash(typeof(HealthCheckRequest).GetTypeFullName());

        // Start "IdentityServer" thin client — handles incoming requests
        _thinServiceClient = new BoltClient(
            thinServerUri, identityServerServiceId, "IdentityServer.Bench",
            config, loggerFactory.CreateLogger<BoltClient>());

        // Register the HealthCheck handler on the service client
        _thinServiceClient.RegisterHandler(typeof(HealthCheckRequest).GetTypeFullName(),
            async (payload, requestId) =>
            {
                var request = MemoryPackSerializer.Deserialize<HealthCheckRequest>(payload.Span)!;

                // Call the endpoint directly (same as what the generated Bolt handler does)
                var result = await IdentityServer.Api.Features.Health.Check.HealthCheckEndpoint.Handle(request, CancellationToken.None);

                var response = new QueryResponse<HealthCheckResponse>
                {
                    HttpStatusCode = (System.Net.HttpStatusCode)result.StatusCode,
                    Response = result.Data,
                    Message = result.Message
                };
                var responseBytes = MemoryPackSerializer.Serialize(response);
                return ((System.Net.HttpStatusCode)result.StatusCode, (ReadOnlyMemory<byte>)responseBytes);
            });

        await _thinServiceClient.ConnectAsync();

        // Start "BenchClient" thin client — sends requests
        _thinCallerClient = new BoltClient(
            thinServerUri, "bench_caller", "BenchClient.Thin",
            config, loggerFactory.CreateLogger<BoltClient>());
        await _thinCallerClient.ConnectAsync();

        // Warmup thin path
        for (var i = 0; i < 5; i++)
        {
            var warmupPayload = MemoryPackSerializer.Serialize(_request);
            await _thinCallerClient.InvokeAsync(
                identityServerServiceId,
                typeof(HealthCheckRequest).GetTypeFullName(),
                warmupPayload);
        }
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _testClientScope?.Dispose();
        _httpClient?.Dispose();
        _grpcChannel?.Dispose();
        try { await _grpcHubApp.StopAsync(); } catch { }
        try { await _grpcBackendApp.StopAsync(); } catch { }
        try { await _thinCallerClient.DisposeAsync(); } catch { }
        try { await _thinServiceClient.DisposeAsync(); } catch { }
        try { await _testClientApp.StopAsync(); } catch { }
        try { await _identityServerApp.StopAsync(); } catch { }
        try { await _streamFlowApp.StopAsync(); } catch { }
        await _postgres.DisposeAsync();
    }

    [Benchmark(Baseline = true)]
    public async Task<HttpResponseMessage> Http_HealthCheck()
    {
        var response = await _httpClient.PostAsJsonAsync("/api/health/check", _request);
        response.EnsureSuccessStatusCode();
        return response;
    }

    [Benchmark]
    public async Task<QueryResponse<HealthCheckResponse>?> Bolt_HealthCheck()
    {
        var req = new HealthCheckRequest
        {
            Metadata = new RequestMetadata
            {
                RequestId = Guid.NewGuid(),
                IpAddress = "127.0.0.1",
                OperationName = "Benchmark",
                DeviceName = "BenchDevice",
                UserAgent = "BenchAgent"
            }
        };
        var response = await _serviceWrapper.HealthCheck(req);
        ValidateBoltResponse(response);
        return response;
    }

    [Benchmark]
    public async Task<HealthCheckResp> Grpc_HealthCheck()
    {
        var response = await _grpcClient.CheckAsync(new HealthCheckReq
        {
            TenantId = TestTenantId.ToString(),
            RequestId = Guid.NewGuid().ToString(),
            IpAddress = "127.0.0.1",
            Name = "Benchmark"
        });
        ValidateGrpcResponse(response);
        return response;
    }

    [Benchmark]
    public async Task<QueryResponse<HealthCheckResponse>?> ThinProtocol_HealthCheck()
    {
        var req = new HealthCheckRequest
        {
            Metadata = new RequestMetadata
            {
                RequestId = Guid.NewGuid(),
                IpAddress = "127.0.0.1",
                OperationName = "Benchmark",
                DeviceName = "BenchDevice",
                UserAgent = "BenchAgent"
            }
        };

        var payload = MemoryPackSerializer.Serialize(req);
        var (statusCode, data) = await _thinCallerClient.InvokeAsync(
            "3902761a822d4c6b8e2d323fd501bcd6",
            typeof(HealthCheckRequest).GetTypeFullName(),
            payload);

        if (statusCode != System.Net.HttpStatusCode.OK)
            throw new InvalidOperationException($"Thin Bolt returned {statusCode}.");

        var response = MemoryPackSerializer.Deserialize<QueryResponse<HealthCheckResponse>>(data.Span);
        ValidateBoltResponse(response);
        return response;
    }

    private static void ValidateBoltResponse(QueryResponse<HealthCheckResponse>? response)
    {
        if (response?.Response is null || response.HttpStatusCode != System.Net.HttpStatusCode.OK ||
            !string.Equals(response.Response.Status, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bolt returned an invalid health response.");
    }

    private static void ValidateGrpcResponse(HealthCheckResp response)
    {
        if (!string.Equals(response.Status, "Healthy", StringComparison.Ordinal))
            throw new InvalidOperationException("gRPC returned an invalid health response.");
    }

    #region Infrastructure

    private WebApplication StartBolt()
    {
        var builder = XApplication.Configure<Bolt.Hub.Installers.BoltInstaller>();
        builder.WebHost.UseUrls(BoltUrl);
        OverrideConfig(builder, "Bolt.Bench", "00000000-0000-0000-0000-000000000099");

        var app = (WebApplication)builder.Build();
        app.UseCorrelationId();
        app.UseAppServices();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _streamFlowTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private WebApplication StartIdentityServer()
    {
        var builder = XApplication.Configure<AuthService>();
        builder.WebHost.UseUrls(IdentityServerUrl);
        OverrideConfig(builder, "IdentityServer.Bench", "3902761a-822d-4c6b-8e2d-323fd501bcd6");
        builder.Configuration["BoltConfiguration:ServerUrls:0"] = BoltServerUrl;

        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddValidatorsFromAssemblyContaining<AuthService>();

        var app = (WebApplication)builder.Build();
        app.UseCorrelationId();
        app.MapGeneratedEndpoints();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _identityServerTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private WebApplication StartTestClient()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(TestClientUrl);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BoltConfiguration:ClientName"] = "BenchClient",
            ["BoltConfiguration:ClientGuid"] = Guid.NewGuid().ToString(),
            ["BoltConfiguration:ServerUrls:0"] = BoltServerUrl,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Logging:LogLevel:Default"] = "Error",
        });

        // NOTE(Task13): Test client uses thin-protocol BoltDriver. IdentityServer still uses
        // SignalR for handler registration, so Bolt benchmarks will time out until Task 13.
        builder.Services.InstallStandardServices<TransportBenchmarks>(builder.Configuration);
        builder.Services.AddSingleton(new DeviceAgentProvider("Benchmark"));
        builder.Services.AddXFrameworkBoltClient(builder.Configuration);
        builder.Services.AddIdentityServerWrapperServices();

        var app = builder.Build();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _testClientTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private async Task WaitForBoltClients()
    {
        var testClientBolt = _testClientApp.Services.GetRequiredService<BoltClient>();

        // Handler registration is now automatic via BoltHandlerRegistrationHostedService
        // when AddXFrameworkBoltClient() is called in the service's startup.
        await Task.Delay(2000);

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (testClientBolt.IsConnected)
            {
                await Task.Delay(1000);
                return;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException("Bolt test client failed to connect within 15s");
    }

    private void OverrideConfig(WebApplicationBuilder builder, string clientName, string clientGuid)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultDatabaseConnection"] = _postgres.GetConnectionString(),
            ["BoltConfiguration:ClientGuid"] = clientGuid,
            ["BoltConfiguration:ClientName"] = clientName,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["JwtOptions:ValidAudience"] = IdentityServerUrl,
            ["JwtOptions:ValidIssuer"] = IdentityServerUrl,
            ["JwtOptions:GenerationId"] = "benchmarks-g1",
            ["JwtOptions:SigningPrivateKeyPath"] = BenchmarkJwtKeyMaterial.PrivateKeyPath,
            ["JwtOptions:SigningPublicKeyPath"] = BenchmarkJwtKeyMaterial.PublicKeyPath,
            ["JwtOptions:AccessTokenLifespan"] = "00:30:00",
            ["JwtOptions:RefreshTokenLifespan"] = "00:30:00",
            ["Logging:LogLevel:Default"] = "Error"
        });
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

    #endregion
}

file class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        AddJob(Job.ShortRun
            .WithWarmupCount(3)
            .WithIterationCount(10));
        AddColumn(StatisticColumn.P95);
        AddColumn(new OpsPerSecColumn());
    }
}

internal class OpsPerSecColumn : IColumn
{
    public string Id => "OpsPerSec";
    public string ColumnName => "Op/s";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => 0;
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "Operations per second (1 / Mean)";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        => GetValue(summary, benchmarkCase, SummaryStyle.Default);

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        var report = summary[benchmarkCase];
        if (report?.ResultStatistics == null) return "N/A";
        var meanNs = report.ResultStatistics.Mean;
        var opsPerSec = 1_000_000_000.0 / meanNs;
        return opsPerSec.ToString("N0");
    }

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
    public bool IsAvailable(Summary summary) => true;
}
