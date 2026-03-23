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
    private ThinStreamFlowClient _thinServiceClient = null!;
    private ThinStreamFlowClient _thinCallerClient = null!;

    // gRPC
    private WebApplication _grpcApp = null!;
    private GrpcChannel _grpcChannel = null!;
    private HealthService.HealthServiceClient _grpcClient = null!;

    // QUIC
    private QuicDirectServer _quicServer = null!;
    private QuicDirectClient _quicClient = null!;

    private const string StreamFlowUrl = "http://localhost:19300";
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

        // 3. StreamFlow hub
        var sfBuilder = XApplication.Configure<StreamFlow.Stream.Installers.StreamInstaller>();
        sfBuilder.WebHost.UseUrls(StreamFlowUrl);
        OverrideConfig(sfBuilder, "StreamFlow.ConcBench", "00000000-0000-0000-0000-000000000098");
        _streamFlowApp = (WebApplication)sfBuilder.Build();
        _streamFlowApp.UseCorrelationId();
        _streamFlowApp.UseAppServices();
        _streamFlowApp.MapGet("/health/live", () => Results.Ok("healthy"));
        _ = Task.Run(() => _streamFlowApp.RunAsync());
        await WaitForHealth($"{StreamFlowUrl}/health/live");

        // 4. IdentityServer
        var idBuilder = XApplication.Configure<AuthService>();
        idBuilder.WebHost.UseUrls(IdentityServerUrl);
        OverrideConfig(idBuilder, "IdentityServer.ConcBench", "3902761a-822d-4c6b-8e2d-323fd501bcd6");
        idBuilder.Configuration["StreamFlowConfiguration:ServerUrls:0"] = $"{StreamFlowUrl}/stream-flow/queue";
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
            ["StreamFlowConfiguration:ClientName"] = "ConcBenchClient",
            ["StreamFlowConfiguration:ServerUrls:0"] = $"{StreamFlowUrl}/stream-flow/queue",
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Serilog:MinimumLevel:Default"] = "Error",
        });
        tcBuilder.Services.InstallStandardServices<ConcurrentBenchmarks>(tcBuilder.Configuration);
        tcBuilder.Services.AddSingleton(new DeviceAgentProvider("Benchmark"));
        tcBuilder.Services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        tcBuilder.Services.AddIdentityServerWrapperServices();
        _testClientApp = tcBuilder.Build();
        _testClientApp.MapGet("/health/live", () => Results.Ok("healthy"));
        _ = Task.Run(() => _testClientApp.RunAsync());
        await WaitForHealth($"{TestClientUrl}/health/live");

        // 7. Wait for StreamFlow clients
        var idSignalR = _identityServerApp.Services.GetRequiredService<ISignalRService>();
        var tcSignalR = _testClientApp.Services.GetRequiredService<ISignalRService>();
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (idSignalR.Connection?.State == HubConnectionState.Connected &&
                tcSignalR.Connection?.State == HubConnectionState.Connected)
            { await Task.Delay(1000); break; }
            await Task.Delay(250);
        }

        // 8. Register StreamFlow handlers
        var logger = _identityServerApp.Services.GetRequiredService<ILogger<BaseSignalRHandler>>();
        var scopeFactory = _identityServerApp.Services.GetRequiredService<IServiceScopeFactory>();
        foreach (var handler in typeof(AuthService).Assembly.GetExportedTypes()
            .Where(t => typeof(ISignalREventHandler).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(Activator.CreateInstance).Cast<ISignalREventHandler>())
            handler.Handle(idSignalR.Connection!, logger, scopeFactory);

        _httpClient = new HttpClient { BaseAddress = new Uri(IdentityServerUrl) };
        _serviceWrapper = _testClientApp.Services.GetRequiredService<IIdentityServerServiceWrapper>();

        // 9. Thin protocol
        await SetupThinProtocol();

        // 10. gRPC
        await SetupGrpc();

        // 11. QUIC
        await SetupQuic();
    }

    private async Task SetupThinProtocol()
    {
        var thinServerUri = new Uri($"ws://localhost:19300/streamflow/ws");
        var config = new StreamFlowConfiguration { RpcTimeoutSeconds = 30 };
        var lf = _streamFlowApp.Services.GetRequiredService<ILoggerFactory>();
        var serviceId = "3902761a822d4c6b8e2d323fd501bcd6";

        _thinServiceClient = new ThinStreamFlowClient(thinServerUri, serviceId, "IdentityServer.ConcBench",
            config, lf.CreateLogger<ThinStreamFlowClient>());
        _thinServiceClient.RegisterHandler(typeof(HealthCheckRequest).GetTypeFullName(), HealthCheckHandler);
        await _thinServiceClient.ConnectAsync();

        _thinCallerClient = new ThinStreamFlowClient(thinServerUri, "conc_bench_caller", "ConcBenchClient.Thin",
            config, lf.CreateLogger<ThinStreamFlowClient>());
        await _thinCallerClient.ConnectAsync();
    }

    private async Task SetupGrpc()
    {
        var grpcBuilder = WebApplication.CreateBuilder();
        grpcBuilder.WebHost.ConfigureKestrel(o =>
        {
            o.ListenLocalhost(19363, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(19364, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        grpcBuilder.Services.AddGrpc();
        grpcBuilder.Logging.SetMinimumLevel(LogLevel.Error);
        _grpcApp = grpcBuilder.Build();
        _grpcApp.MapGrpcService<GrpcHealthServiceImpl>();
        _grpcApp.MapGet("/health/live", () => Results.Ok("healthy"));
        _ = Task.Run(() => _grpcApp.RunAsync());
        await WaitForHealth("http://localhost:19364/health/live");
        _grpcChannel = GrpcChannel.ForAddress(GrpcUrl);
        _grpcClient = new HealthService.HealthServiceClient(_grpcChannel);
    }

    private async Task SetupQuic()
    {
        if (!QuicListener.IsSupported) return;
        var ep = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 19365);
        var lf = _streamFlowApp.Services.GetRequiredService<ILoggerFactory>();
        _quicServer = new QuicDirectServer(lf.CreateLogger<QuicDirectServer>());
        _quicServer.RegisterHandler(typeof(HealthCheckRequest).GetTypeFullName(), HealthCheckHandler);
        await _quicServer.StartAsync(ep);
        _quicClient = new QuicDirectClient(ep);
        await _quicClient.ConnectAsync();
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
    public async Task StreamFlow_Concurrent()
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
    public async Task Quic_Concurrent()
    {
        var tasks = new Task[Concurrency];
        for (int i = 0; i < Concurrency; i++)
        {
            var payload = MemoryPackSerializer.Serialize(MakeRequest());
            tasks[i] = _quicClient.InvokeAsync(typeof(HealthCheckRequest).GetTypeFullName(), payload);
        }
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
        try { await _quicClient.DisposeAsync(); } catch { }
        try { await _quicServer.DisposeAsync(); } catch { }
        try { await _grpcApp.StopAsync(); } catch { }
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
