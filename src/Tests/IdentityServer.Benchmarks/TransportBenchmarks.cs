using System.Net.Http.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;
using FluentValidation;
using IdentityServer.Api.Generated;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using MemoryPack;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using StreamFlow.Domain.Shared.Protocol;
using StreamFlow.Stream.Extensions;
using StreamFlow.Stream.ThinProtocol;
using Testcontainers.PostgreSql;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Configurations;
using XFramework.Domain.Shared.Extensions;
using XFramework.Domain.Shared.Interfaces;
using IdentityServer.Domain.Shared.Contracts;
using XFramework.Extensions;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using XFramework.Integration.ThinProtocol;
using System.Net.Quic;
using Grpc.Net.Client;
using IdentityServer.Benchmarks.Grpc;
using StreamFlow.Stream.ThinProtocol;
using Contracts = IdentityServer.Domain.Shared.Contracts;

namespace IdentityServer.Benchmarks;

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

    // QUIC (direct — no hub, server-to-server)
    private QuicDirectServer _quicServer = null!;
    private QuicDirectClient _quicClient = null!;

    private const string StreamFlowUrl = "http://localhost:19000";
    private const string IdentityServerUrl = "http://localhost:19261";
    private const string TestClientUrl = "http://localhost:19262";
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

        // 3. Start StreamFlow hub
        _streamFlowApp = StartStreamFlow();
        await WaitForHealth($"{StreamFlowUrl}/health/live");

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

        // 6. Start test client (StreamFlow consumer)
        _testClientApp = StartTestClient();
        await WaitForHealth($"{TestClientUrl}/health/live");

        // 7. Wait for StreamFlow clients to connect
        await WaitForStreamFlowClients();

        // 8. Register StreamFlow handlers
        RegisterStreamFlowHandlers();

        // 9. Create reusable objects
        _httpClient = new HttpClient { BaseAddress = new Uri(IdentityServerUrl) };
        _serviceWrapper = _testClientApp.Services.GetRequiredService<IIdentityServerServiceWrapper>();
        _request = new HealthCheckRequest
        {
            Metadata = new RequestMetadata
            {
                TenantId = TestTenantId,
                RequestId = Guid.NewGuid(),
                IpAddress = "127.0.0.1",
                Name = "Benchmark",
                DeviceName = "BenchDevice",
                DeviceAgent = "BenchAgent"
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
                    TenantId = TestTenantId, RequestId = Guid.NewGuid(),
                    IpAddress = "127.0.0.1", Name = "Benchmark",
                    DeviceName = "BenchDevice", DeviceAgent = "BenchAgent"
                }
            });
        }

        // 10. Setup thin protocol clients
        await SetupThinProtocol();

        // 11. Setup gRPC (benchmark-only)
        await SetupGrpc();

        // 12. Setup QUIC
        await SetupQuic();
    }

    private async Task SetupQuic()
    {
        if (!QuicListener.IsSupported)
        {
            Console.WriteLine("QUIC not supported on this platform, skipping QUIC benchmark");
            return;
        }

        var quicEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 19265);
        var loggerFactory = _streamFlowApp.Services.GetRequiredService<ILoggerFactory>();

        // Direct QUIC server — handles requests directly (no hub routing)
        _quicServer = new QuicDirectServer(loggerFactory.CreateLogger<QuicDirectServer>());
        _quicServer.RegisterHandler(typeof(HealthCheckRequest).GetTypeFullName(),
            async (payload, requestId) =>
            {
                var request = MemoryPackSerializer.Deserialize<HealthCheckRequest>(payload.Span)!;
                var result = await IdentityServer.Api.Features.Health.Check.HealthCheckEndpoint.Handle(request, CancellationToken.None);
                var response = new QueryResponse<HealthCheckResponse>
                {
                    HttpStatusCode = (System.Net.HttpStatusCode)result.StatusCode,
                    Response = result.Data,
                    Message = result.Message
                };
                return ((System.Net.HttpStatusCode)result.StatusCode, (ReadOnlyMemory<byte>)MemoryPackSerializer.Serialize(response));
            });
        await _quicServer.StartAsync(quicEndPoint);

        // Direct QUIC client
        _quicClient = new QuicDirectClient(quicEndPoint);
        await _quicClient.ConnectAsync();

        // Warmup
        for (var i = 0; i < 5; i++)
        {
            var warmupPayload = MemoryPackSerializer.Serialize(_request);
            await _quicClient.InvokeAsync(typeof(HealthCheckRequest).GetTypeFullName(), warmupPayload);
        }
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
        var thinServerUri = new Uri($"ws://localhost:19000/streamflow/ws");
        var config = new StreamFlowConfiguration { RpcTimeoutSeconds = 30 };
        var loggerFactory = _streamFlowApp.Services.GetRequiredService<ILoggerFactory>();

        // Compute hashes for routing
        var identityServerServiceId = "3902761a822d4c6b8e2d323fd501bcd6"; // SHA256 of "IdentityServer" — same as SignalR registration
        _identityServerServiceHash = StreamFlowCodec.Fnv1aHash(identityServerServiceId);
        _healthCheckCommandHash = StreamFlowCodec.Fnv1aHash(typeof(HealthCheckRequest).GetTypeFullName());

        // Start "IdentityServer" thin client — handles incoming requests
        _thinServiceClient = new BoltClient(
            thinServerUri, identityServerServiceId, "IdentityServer.Bench",
            config, loggerFactory.CreateLogger<BoltClient>());

        // Register the HealthCheck handler on the service client
        _thinServiceClient.RegisterHandler(typeof(HealthCheckRequest).GetTypeFullName(),
            async (payload, requestId) =>
            {
                var request = MemoryPackSerializer.Deserialize<HealthCheckRequest>(payload.Span)!;

                // Call the endpoint directly (same as what the generated StreamFlow handler does)
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
        _httpClient?.Dispose();
        _grpcChannel?.Dispose();
        try { await _quicClient.DisposeAsync(); } catch { }
        try { await _quicServer.DisposeAsync(); } catch { }
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
        return await _httpClient.PostAsJsonAsync("/api/health/check", _request);
    }

    [Benchmark]
    public async Task<QueryResponse<HealthCheckResponse>?> StreamFlow_HealthCheck()
    {
        var req = new HealthCheckRequest
        {
            Metadata = new RequestMetadata
            {
                TenantId = TestTenantId,
                RequestId = Guid.NewGuid(),
                IpAddress = "127.0.0.1",
                Name = "Benchmark",
                DeviceName = "BenchDevice",
                DeviceAgent = "BenchAgent"
            }
        };
        return await _serviceWrapper.HealthCheck(req);
    }

    [Benchmark]
    public async Task<HealthCheckResp> Grpc_HealthCheck()
    {
        return await _grpcClient.CheckAsync(new HealthCheckReq
        {
            TenantId = TestTenantId.ToString(),
            RequestId = Guid.NewGuid().ToString(),
            IpAddress = "127.0.0.1",
            Name = "Benchmark"
        });
    }

    [Benchmark]
    public async Task<QueryResponse<HealthCheckResponse>?> Quic_HealthCheck()
    {
        var req = new HealthCheckRequest
        {
            Metadata = new RequestMetadata
            {
                TenantId = TestTenantId,
                RequestId = Guid.NewGuid(),
                IpAddress = "127.0.0.1",
                Name = "Benchmark",
                DeviceName = "BenchDevice",
                DeviceAgent = "BenchAgent"
            }
        };

        var payload = MemoryPackSerializer.Serialize(req);
        var (statusCode, data) = await _quicClient.InvokeAsync(
            typeof(HealthCheckRequest).GetTypeFullName(), payload);

        return MemoryPackSerializer.Deserialize<QueryResponse<HealthCheckResponse>>(data.Span);
    }

    [Benchmark]
    public async Task<QueryResponse<HealthCheckResponse>?> ThinProtocol_HealthCheck()
    {
        var req = new HealthCheckRequest
        {
            Metadata = new RequestMetadata
            {
                TenantId = TestTenantId,
                RequestId = Guid.NewGuid(),
                IpAddress = "127.0.0.1",
                Name = "Benchmark",
                DeviceName = "BenchDevice",
                DeviceAgent = "BenchAgent"
            }
        };

        var payload = MemoryPackSerializer.Serialize(req);
        var (statusCode, data) = await _thinCallerClient.InvokeAsync(
            "3902761a822d4c6b8e2d323fd501bcd6",
            typeof(HealthCheckRequest).GetTypeFullName(),
            payload);

        return MemoryPackSerializer.Deserialize<QueryResponse<HealthCheckResponse>>(data.Span);
    }

    #region Infrastructure

    private WebApplication StartStreamFlow()
    {
        var builder = XApplication.Configure<StreamFlow.Stream.Installers.StreamInstaller>();
        builder.WebHost.UseUrls(StreamFlowUrl);
        OverrideConfig(builder, "StreamFlow.Bench", "00000000-0000-0000-0000-000000000099");

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
        builder.Configuration["StreamFlowConfiguration:ServerUrls:0"] = $"{StreamFlowUrl}/stream-flow/queue";

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
            ["StreamFlowConfiguration:ClientName"] = "BenchClient",
            ["StreamFlowConfiguration:ServerUrls:0"] = $"{StreamFlowUrl}/stream-flow/queue",
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Serilog:MinimumLevel:Default"] = "Error",
        });

        builder.Services.InstallStandardServices<TransportBenchmarks>(builder.Configuration);
        builder.Services.AddSingleton(new DeviceAgentProvider("Benchmark"));
        builder.Services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        builder.Services.AddIdentityServerWrapperServices();

        var app = builder.Build();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _testClientTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private async Task WaitForStreamFlowClients()
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
        throw new TimeoutException("StreamFlow clients failed to connect within 15s");
    }

    private void RegisterStreamFlowHandlers()
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

    private void OverrideConfig(WebApplicationBuilder builder, string clientName, string clientGuid)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultDatabaseConnection"] = _postgres.GetConnectionString(),
            ["StreamFlowConfiguration:ClientGuid"] = clientGuid,
            ["StreamFlowConfiguration:ClientName"] = clientName,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["JwtOptions:ValidAudience"] = IdentityServerUrl,
            ["JwtOptions:ValidIssuer"] = IdentityServerUrl,
            ["JwtOptions:Secret"] = "Mm1VFHaqZ7MoVJyZd1zrAKxTpsXbYG6RqSMKYG2cV7RBBUdmsm97HOfKyA7MZ1LUl77ZklJPJfnegohyHqJIoQ983fTKmJcY",
            ["JwtOptions:AccessTokenLifespan"] = "00:30:00",
            ["JwtOptions:RefreshTokenLifespan"] = "00:30:00",
            ["Serilog:MinimumLevel:Default"] = "Error"
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
