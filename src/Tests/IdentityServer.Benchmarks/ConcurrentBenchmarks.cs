using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Bolt.Client;
using FluentValidation;
using Grpc.Net.Client;
using IdentityServer.Api.Generated;
using IdentityServer.Api.Services;
using IdentityServer.Benchmarks.Grpc;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using MemoryPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
using Contracts = IdentityServer.Domain.Shared.Contracts;

namespace IdentityServer.Benchmarks;

/// <summary>
/// Concurrent load benchmark — measures throughput under parallel RPC calls.
/// Shows how each transport handles contention, multiplexing, and head-of-line blocking.
/// </summary>
[Config(typeof(ConcurrentBenchmarkConfig))]
[MemoryDiagnoser]
public class ConcurrentBenchmarks
{
    private PostgreSqlContainer _postgres = null!;
    private WebApplication _streamFlowApp = null!;
    private WebApplication _identityServerApp = null!;
    private WebApplication _testClientApp = null!;
    private HttpClient _httpClient = null!;
    private IIdentityServerServiceWrapper _serviceWrapper = null!;

    // Thin protocol
    private BoltClient _thinServiceClient = null!;
    private BoltClient _thinCallerClient = null!;

    // gRPC (with hub)
    private WebApplication _grpcBackendApp = null!;
    private WebApplication _grpcHubApp = null!;
    private GrpcChannel _grpcChannel = null!;
    private HealthService.HealthServiceClient _grpcClient = null!;

    private const string BoltUrl = "http://localhost:19300";
    private const string IdentityServerUrl = "http://localhost:19361";
    private const string TestClientUrl = "http://localhost:19362";
    private const string GrpcUrl = "http://localhost:19363";
    private static readonly Guid TestTenantId = Guid.Parse("7602c2d3-01df-4bdb-9a67-02c144e4a2ac");

    [Params(1, 16, 64)]
    public int Concurrency { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        // 1. Start Postgres
        _postgres = new PostgreSqlBuilder()
            .WithDatabase("XFramework_ConcBench")
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

        // 3. Bolt hub
        var sfBuilder = XApplication.Configure<Bolt.Hub.Installers.BoltInstaller>();
        sfBuilder.WebHost.UseUrls(BoltUrl);
        OverrideConfig(sfBuilder, "Bolt.ConcBench", "00000000-0000-0000-0000-000000000098");
        _streamFlowApp = (WebApplication)sfBuilder.Build();
        _streamFlowApp.UseCorrelationId();
        _streamFlowApp.UseAppServices();
        _streamFlowApp.MapGet("/health/live", () => Results.Ok("healthy"));
        _ = Task.Run(() => _streamFlowApp.RunAsync());
        await WaitForHealth($"{BoltUrl}/health/live");

        // 4. IdentityServer
        var idBuilder = XApplication.Configure<AuthService>();
        idBuilder.WebHost.UseUrls(IdentityServerUrl);
        OverrideConfig(idBuilder, "IdentityServer.ConcBench", "3902761a-822d-4c6b-8e2d-323fd501bcd6");
        idBuilder.Configuration["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/bolt/ws";
        idBuilder.Services.AddScoped<IAuthService, AuthService>();
        idBuilder.Services.AddValidatorsFromAssemblyContaining<AuthService>();
        _identityServerApp = (WebApplication)idBuilder.Build();
        _identityServerApp.UseCorrelationId();
        _identityServerApp.MapGeneratedEndpoints();
        _identityServerApp.MapGet("/health/live", () => Results.Ok("healthy"));
        _ = Task.Run(() => _identityServerApp.RunAsync());
        await WaitForHealth($"{IdentityServerUrl}/health/live");

        // 5. Seed tenant cache
        var cache = _identityServerApp.Services.GetRequiredService<IMemoryCache>();
        cache.Set($"GetTenant-{TestTenantId}", new Contracts.Tenant
        {
            Id = TestTenantId, TenantId = TestTenantId,
            Name = "Bench Tenant", Description = "Benchmark tenant"
        });

        // 6. Test client
        var tcBuilder = WebApplication.CreateBuilder();
        tcBuilder.WebHost.UseUrls(TestClientUrl);
        tcBuilder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BoltConfiguration:ClientName"] = "ConcBenchClient",
            ["BoltConfiguration:ClientGuid"] = Guid.NewGuid().ToString(),
            ["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/bolt/ws",
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Serilog:MinimumLevel:Default"] = "Error",
        });
        // NOTE(Task13): Test client uses thin-protocol BoltDriver. IdentityServer still uses
        // SignalR for handler registration, so Bolt benchmarks will time out until Task 13.
        tcBuilder.Services.InstallStandardServices<ConcurrentBenchmarks>(tcBuilder.Configuration);
        tcBuilder.Services.AddSingleton(new DeviceAgentProvider("Benchmark"));
        tcBuilder.Services.AddXFrameworkBoltClient(tcBuilder.Configuration);
        tcBuilder.Services.AddIdentityServerWrapperServices();
        _testClientApp = tcBuilder.Build();
        _testClientApp.MapGet("/health/live", () => Results.Ok("healthy"));
        _ = Task.Run(() => _testClientApp.RunAsync());
        await WaitForHealth($"{TestClientUrl}/health/live");

        // 7. Wait for Bolt clients
        // Handler registration is now automatic via BoltHandlerRegistrationHostedService.
        var tcBoltClient = _testClientApp.Services.GetRequiredService<BoltClient>();
        await Task.Delay(2000);
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (tcBoltClient.IsConnected)
            { await Task.Delay(1000); break; }
            await Task.Delay(250);
        }

        _httpClient = new HttpClient { BaseAddress = new Uri(IdentityServerUrl) };
        _serviceWrapper = _testClientApp.Services.GetRequiredService<IIdentityServerServiceWrapper>();

        // 9. Thin protocol
        await SetupThinProtocol();

        // 10. gRPC
        await SetupGrpc();

    }

    private async Task SetupThinProtocol()
    {
        var thinServerUri = new Uri($"ws://localhost:19300/bolt/ws");
        var config = new BoltClientOptions { RpcTimeoutSeconds = 30 };
        var lf = _streamFlowApp.Services.GetRequiredService<ILoggerFactory>();
        var serviceId = "3902761a822d4c6b8e2d323fd501bcd6";

        _thinServiceClient = new BoltClient(thinServerUri, serviceId, "IdentityServer.ConcBench",
            config, lf.CreateLogger<BoltClient>());
        _thinServiceClient.RegisterHandler(typeof(HealthCheckRequest).GetTypeFullName(), HealthCheckHandler);
        await _thinServiceClient.ConnectAsync();

        _thinCallerClient = new BoltClient(thinServerUri, "conc_bench_caller", "ConcBenchClient.Thin",
            config, lf.CreateLogger<BoltClient>());
        await _thinCallerClient.ConnectAsync();
    }

    private async Task SetupGrpc()
    {
        // Backend
        var backendBuilder = WebApplication.CreateBuilder();
        backendBuilder.WebHost.ConfigureKestrel(o =>
        {
            o.ListenLocalhost(19363, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(19364, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        backendBuilder.Services.AddGrpc();
        backendBuilder.Logging.SetMinimumLevel(LogLevel.Error);
        _grpcBackendApp = backendBuilder.Build();
        _grpcBackendApp.MapGrpcService<GrpcHealthBackend>();
        _grpcBackendApp.MapGet("/health/live", () => Results.Ok("healthy"));
        _ = Task.Run(() => _grpcBackendApp.RunAsync());
        await WaitForHealth("http://localhost:19364/health/live");

        // Hub
        var backendChannel = GrpcChannel.ForAddress("http://localhost:19363");
        var backendClient = new HealthService.HealthServiceClient(backendChannel);
        var hubBuilder = WebApplication.CreateBuilder();
        hubBuilder.WebHost.ConfigureKestrel(o =>
        {
            o.ListenLocalhost(19366, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(19367, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        hubBuilder.Services.AddGrpc();
        hubBuilder.Services.AddSingleton(backendClient);
        hubBuilder.Logging.SetMinimumLevel(LogLevel.Error);
        _grpcHubApp = hubBuilder.Build();
        _grpcHubApp.MapGrpcService<GrpcHealthHub>();
        _grpcHubApp.MapGet("/health/live", () => Results.Ok("healthy"));
        _ = Task.Run(() => _grpcHubApp.RunAsync());
        await WaitForHealth("http://localhost:19367/health/live");

        // Client → Hub
        _grpcChannel = GrpcChannel.ForAddress("http://localhost:19366");
        _grpcClient = new HealthService.HealthServiceClient(_grpcChannel);
    }

    private static async Task<(System.Net.HttpStatusCode, ReadOnlyMemory<byte>)> HealthCheckHandler(ReadOnlyMemory<byte> payload, Guid requestId)
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
    }

    private HealthCheckRequest MakeRequest() => new()
    {
        Metadata = new RequestMetadata
        {
            TenantId = TestTenantId, RequestId = Guid.NewGuid(),
            IpAddress = "127.0.0.1", Name = "Benchmark",
            DeviceName = "BenchDevice", DeviceAgent = "BenchAgent"
        }
    };

    [Benchmark(Baseline = true)]
    public async Task Http_Concurrent()
    {
        var tasks = new Task[Concurrency];
        for (int i = 0; i < Concurrency; i++)
            tasks[i] = _httpClient.PostAsJsonAsync("/api/health/check", MakeRequest());
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task Bolt_Concurrent()
    {
        var tasks = new Task[Concurrency];
        for (int i = 0; i < Concurrency; i++)
            tasks[i] = _serviceWrapper.HealthCheck(MakeRequest());
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task Grpc_Concurrent()
    {
        var tasks = new Task[Concurrency];
        for (int i = 0; i < Concurrency; i++)
            tasks[i] = _grpcClient.CheckAsync(new HealthCheckReq
            {
                TenantId = TestTenantId.ToString(), RequestId = Guid.NewGuid().ToString(),
                IpAddress = "127.0.0.1", Name = "Benchmark"
            }).ResponseAsync;
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task ThinProtocol_Concurrent()
    {
        var tasks = new Task[Concurrency];
        for (int i = 0; i < Concurrency; i++)
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
        try { await _testClientApp.StopAsync(); } catch { }
        try { await _identityServerApp.StopAsync(); } catch { }
        try { await _streamFlowApp.StopAsync(); } catch { }
        await _postgres.DisposeAsync();
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
            try { if ((await client.GetAsync(url)).IsSuccessStatusCode) return; } catch { }
            await Task.Delay(500);
        }
        throw new TimeoutException($"Service at {url} did not become healthy within {timeoutSeconds}s");
    }
}

file class ConcurrentBenchmarkConfig : ManualConfig
{
    public ConcurrentBenchmarkConfig()
    {
        AddJob(Job.ShortRun.WithWarmupCount(2).WithIterationCount(5));
        AddColumn(StatisticColumn.P95);
        AddColumn(new OpsPerSecColumn());
    }
}
