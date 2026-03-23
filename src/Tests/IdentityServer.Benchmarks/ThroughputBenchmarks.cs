using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.Quic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using FluentValidation;
using Grpc.Net.Client;
using IdentityServer.Api.Generated;
using IdentityServer.Api.Services;
using IdentityServer.Benchmarks.Grpc;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using MemoryPack;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Perfolizer.Horology;
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
using XFramework.Extensions;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using XFramework.Integration.ThinProtocol;
using Contracts = IdentityServer.Domain.Shared.Contracts;

namespace IdentityServer.Benchmarks;

/// <summary>
/// Maximum throughput benchmark — fires as many RPCs as possible using
/// Parallel.ForEachAsync to saturate the machine. Each iteration processes
/// a batch of 1000 requests at max parallelism.
///
/// Reports: total batch time and derived ops/sec.
/// </summary>
[Config(typeof(ThroughputConfig))]
[MemoryDiagnoser]
public class ThroughputBenchmarks
{
    private PostgreSqlContainer _postgres = null!;
    private WebApplication _streamFlowApp = null!;
    private WebApplication _identityServerApp = null!;
    private WebApplication _testClientApp = null!;
    private HttpClient _httpClient = null!;

    // Bolt
    private ThinStreamFlowClient _thinServiceClient = null!;
    private ThinStreamFlowClient _thinCallerClient = null!;

    // gRPC (with hub)
    private WebApplication _grpcBackendApp = null!;
    private WebApplication _grpcHubApp = null!;
    private GrpcChannel _grpcChannel = null!;
    private HealthService.HealthServiceClient _grpcClient = null!;

    private const string StreamFlowUrl = "http://localhost:19400";
    private const string IdentityServerUrl = "http://localhost:19461";
    private const string TestClientUrl = "http://localhost:19462";
    private static readonly Guid TestTenantId = Guid.Parse("7602c2d3-01df-4bdb-9a67-02c144e4a2ac");

    private const int BatchSize = 100;

    [GlobalSetup]
    public async Task Setup()
    {
        _postgres = new PostgreSqlBuilder()
            .WithDatabase("XFramework_ThroughputBench")
            .WithUsername("bench_user")
            .WithPassword("bench_password")
            .Build();
        await _postgres.StartAsync();

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

        // StreamFlow hub
        var sfBuilder = XApplication.Configure<StreamFlow.Stream.Installers.StreamInstaller>();
        sfBuilder.WebHost.UseUrls(StreamFlowUrl);
        OverrideConfig(sfBuilder, "StreamFlow.TpBench", "00000000-0000-0000-0000-000000000097");
        _streamFlowApp = (WebApplication)sfBuilder.Build();
        _streamFlowApp.UseCorrelationId();
        _streamFlowApp.UseAppServices();
        _streamFlowApp.MapGet("/health/live", () => Results.Ok("healthy"));
        _ = Task.Run(() => _streamFlowApp.RunAsync());
        await WaitForHealth($"{StreamFlowUrl}/health/live");

        // IdentityServer
        var idBuilder = XApplication.Configure<AuthService>();
        idBuilder.WebHost.UseUrls(IdentityServerUrl);
        OverrideConfig(idBuilder, "IdentityServer.TpBench", "3902761a-822d-4c6b-8e2d-323fd501bcd6");
        idBuilder.Configuration["StreamFlowConfiguration:ServerUrls:0"] = $"{StreamFlowUrl}/stream-flow/queue";
        idBuilder.Services.AddScoped<IAuthService, AuthService>();
        idBuilder.Services.AddValidatorsFromAssemblyContaining<AuthService>();
        _identityServerApp = (WebApplication)idBuilder.Build();
        _identityServerApp.UseCorrelationId();
        _identityServerApp.MapGeneratedEndpoints();
        _identityServerApp.MapGet("/health/live", () => Results.Ok("healthy"));
        _ = Task.Run(() => _identityServerApp.RunAsync());
        await WaitForHealth($"{IdentityServerUrl}/health/live");

        var cache = _identityServerApp.Services.GetRequiredService<IMemoryCache>();
        cache.Set($"GetTenant-{TestTenantId}", new Contracts.Tenant
        {
            Id = TestTenantId, TenantId = TestTenantId,
            Name = "Bench Tenant", Description = "Benchmark tenant"
        });

        _httpClient = new HttpClient { BaseAddress = new Uri(IdentityServerUrl) };

        // Bolt
        await SetupBolt();

        // gRPC with hub
        await SetupGrpc();

        // Warmup all paths
        await WarmupAll();
    }

    private async Task SetupBolt()
    {
        var thinServerUri = new Uri($"ws://localhost:19400/streamflow/ws");
        var config = new StreamFlowConfiguration { RpcTimeoutSeconds = 60 };
        var lf = _streamFlowApp.Services.GetRequiredService<ILoggerFactory>();
        var serviceId = "3902761a822d4c6b8e2d323fd501bcd6";

        _thinServiceClient = new ThinStreamFlowClient(thinServerUri, serviceId, "IdentityServer.TpBench",
            config, lf.CreateLogger<ThinStreamFlowClient>());
        _thinServiceClient.RegisterHandler(typeof(HealthCheckRequest).GetTypeFullName(), HealthCheckHandler);
        await _thinServiceClient.ConnectAsync();

        // Caller: multiple connections for throughput
        _thinCallerClient = new ThinStreamFlowClient(thinServerUri, "tp_bench_caller", "TpBenchClient.Bolt",
            config, lf.CreateLogger<ThinStreamFlowClient>());
        await _thinCallerClient.ConnectAsync();
    }

    private async Task SetupGrpc()
    {
        var backendBuilder = WebApplication.CreateBuilder();
        backendBuilder.WebHost.ConfigureKestrel(o =>
        {
            o.ListenLocalhost(19463, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(19464, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        backendBuilder.Services.AddGrpc();
        backendBuilder.Logging.SetMinimumLevel(LogLevel.Error);
        _grpcBackendApp = backendBuilder.Build();
        _grpcBackendApp.MapGrpcService<GrpcHealthBackend>();
        _grpcBackendApp.MapGet("/health/live", () => Results.Ok("healthy"));
        _ = Task.Run(() => _grpcBackendApp.RunAsync());
        await WaitForHealth("http://localhost:19464/health/live");

        var backendChannel = GrpcChannel.ForAddress("http://localhost:19463");
        var backendClient = new HealthService.HealthServiceClient(backendChannel);

        var hubBuilder = WebApplication.CreateBuilder();
        hubBuilder.WebHost.ConfigureKestrel(o =>
        {
            o.ListenLocalhost(19466, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(19467, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        hubBuilder.Services.AddGrpc();
        hubBuilder.Services.AddSingleton(backendClient);
        hubBuilder.Logging.SetMinimumLevel(LogLevel.Error);
        _grpcHubApp = hubBuilder.Build();
        _grpcHubApp.MapGrpcService<GrpcHealthHub>();
        _grpcHubApp.MapGet("/health/live", () => Results.Ok("healthy"));
        _ = Task.Run(() => _grpcHubApp.RunAsync());
        await WaitForHealth("http://localhost:19467/health/live");

        _grpcChannel = GrpcChannel.ForAddress("http://localhost:19466");
        _grpcClient = new HealthService.HealthServiceClient(_grpcChannel);
    }

    private async Task WarmupAll()
    {
        var req = MakeRequest();
        for (int i = 0; i < 10; i++)
        {
            await _httpClient.PostAsJsonAsync("/api/health/check", req);
            await _grpcClient.CheckAsync(new HealthCheckReq { TenantId = TestTenantId.ToString(), RequestId = Guid.NewGuid().ToString() });
            await _thinCallerClient.InvokeAsync("3902761a822d4c6b8e2d323fd501bcd6",
                typeof(HealthCheckRequest).GetTypeFullName(), MemoryPackSerializer.Serialize(req));
        }
    }

    private static async Task<(System.Net.HttpStatusCode, ReadOnlyMemory<byte>)> HealthCheckHandler(ReadOnlyMemory<byte> payload, Guid requestId)
    {
        var request = MemoryPackSerializer.Deserialize<HealthCheckRequest>(payload.Span)!;
        var result = await IdentityServer.Api.Features.Health.Check.HealthCheckEndpoint.Handle(request, CancellationToken.None);
        var response = new QueryResponse<HealthCheckResponse>
        {
            HttpStatusCode = (System.Net.HttpStatusCode)result.StatusCode,
            Response = result.Data
        };
        return ((System.Net.HttpStatusCode)result.StatusCode, (ReadOnlyMemory<byte>)MemoryPackSerializer.Serialize(response));
    }

    private HealthCheckRequest MakeRequest() => new()
    {
        Metadata = new RequestMetadata
        {
            TenantId = TestTenantId, RequestId = Guid.NewGuid(),
            IpAddress = "127.0.0.1", Name = "Benchmark"
        }
    };

    /// <summary>
    /// Max throughput: fire 1000 requests at max parallelism.
    /// BenchmarkDotNet measures total batch time. Derived ops/sec = 1000 / mean_seconds.
    /// </summary>
    /// <summary>
    /// Fire BatchSize requests concurrently via Task.WhenAll (same pattern as concurrent benchmark).
    /// OperationsPerInvoke tells BenchmarkDotNet to divide time by BatchSize for per-op metrics.
    /// </summary>
    [Benchmark(Baseline = true, OperationsPerInvoke = BatchSize)]
    public async Task Http_MaxThroughput()
    {
        var tasks = new Task[BatchSize];
        for (int i = 0; i < BatchSize; i++)
            tasks[i] = _httpClient.PostAsJsonAsync("/api/health/check", MakeRequest());
        await Task.WhenAll(tasks);
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public async Task Grpc_MaxThroughput()
    {
        var tasks = new Task[BatchSize];
        for (int i = 0; i < BatchSize; i++)
            tasks[i] = _grpcClient.CheckAsync(new HealthCheckReq
            {
                TenantId = TestTenantId.ToString(),
                RequestId = Guid.NewGuid().ToString()
            }).ResponseAsync;
        await Task.WhenAll(tasks);
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public async Task Bolt_MaxThroughput()
    {
        var tasks = new Task[BatchSize];
        for (int i = 0; i < BatchSize; i++)
        {
            var payload = MemoryPackSerializer.Serialize(MakeRequest());
            tasks[i] = _thinCallerClient.InvokeAsync(
                "3902761a822d4c6b8e2d323fd501bcd6",
                typeof(HealthCheckRequest).GetTypeFullName(), payload);
        }
        await Task.WhenAll(tasks);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _httpClient?.Dispose();
        _grpcChannel?.Dispose();
        try { await _grpcHubApp.StopAsync(); } catch { }
        try { await _grpcBackendApp.StopAsync(); } catch { }
        try { await _thinCallerClient.DisposeAsync(); } catch { }
        try { await _thinServiceClient.DisposeAsync(); } catch { }
        try { await _identityServerApp.StopAsync(); } catch { }
        try { await _streamFlowApp.StopAsync(); } catch { }
        await _postgres.DisposeAsync();
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
            ["Serilog:MinimumLevel:Default"] = "Error"
        });
    }

    private static async Task WaitForHealth(string url, int timeoutSeconds = 30)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try { if ((await client.GetAsync(url)).IsSuccessStatusCode) return; } catch { }
            await Task.Delay(500);
        }
        throw new TimeoutException($"Service at {url} did not become healthy within {timeoutSeconds}s");
    }
}

file class ThroughputConfig : ManualConfig
{
    public ThroughputConfig()
    {
        AddJob(Job.ShortRun.WithWarmupCount(2).WithIterationCount(5));
        AddColumn(StatisticColumn.P95);
        AddColumn(new OpsPerSecColumn());
    }
}
