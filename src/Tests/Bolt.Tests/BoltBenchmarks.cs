using System.Net;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Bolt.Client;
using Bolt.Server;
using Bolt.Tests.Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using MemoryPack;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Bolt.Tests;

/// <summary>
/// Standalone transport comparison: Bolt Hub, Bolt Direct, gRPC (with hub), SignalR (with hub).
/// All transports return a simple "Hello {name}" string.
/// No XFramework dependencies — pure protocol benchmark.
/// </summary>
[Config(typeof(BoltBenchConfig))]
[MemoryDiagnoser]
public class BoltBenchmarks
{
    // Bolt Hub
    private WebApplication _boltHubApp = null!;
    private BoltClient _boltHubService = null!;
    private BoltClient _boltHubCaller = null!;

    // Bolt Direct
    private WebApplication _boltDirectApp = null!;
    private BoltClient _boltDirectClient = null!;

    // gRPC Hub (client → hub → backend → hub → client)
    private WebApplication _grpcBackendApp = null!;
    private WebApplication _grpcHubApp = null!;
    private GrpcChannel _grpcChannel = null!;
    private HelloService.HelloServiceClient _grpcClient = null!;

    // gRPC Direct (client → server, no hub)
    private WebApplication _grpcDirectApp = null!;
    private GrpcChannel _grpcDirectChannel = null!;
    private HelloService.HelloServiceClient _grpcDirectClient = null!;

    // SignalR (with hub routing: client → hub → backend → hub → client)
    private WebApplication _signalRBackendApp = null!;
    private WebApplication _signalRHubApp = null!;
    private HubConnection _signalRCaller = null!;

    [Params(1, 64)]
    public int Concurrency { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        await SetupBoltHub();
        await SetupBoltDirect();
        await SetupGrpc();
        await SetupGrpcDirect();
        await SetupSignalR();

        // Warmup all paths
        for (int i = 0; i < 10; i++)
        {
            await BoltHubCall();
            await BoltDirectCall();
            await GrpcCall();
            await GrpcDirectCall();
            await SignalRCall();
        }
    }

    #region Setup

    private async Task SetupBoltHub()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://localhost:18100");
        builder.Services.AddSingleton<BoltServer>();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        _boltHubApp = builder.Build();
        _boltHubApp.UseWebSockets();
        _boltHubApp.MapBolt("/bolt");
        _boltHubApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _boltHubApp.RunAsync());
        await WaitForHealth("http://localhost:18100/health");

        var lf = _boltHubApp.Services.GetRequiredService<ILoggerFactory>();
        // Multiple connections for both caller and service — hub round-robins across them
        var opts = new BoltClientOptions { RpcTimeoutSeconds = 30, MinConnections = 4, MaxConnections = 8, ScaleUpThreshold = 16 };

        _boltHubService = new BoltClient(new Uri("ws://localhost:18100/bolt"),
            "bolt_service", "BoltService", opts, lf.CreateLogger<BoltClient>());
        _boltHubService.RegisterHandler("hello", HelloHandler);
        await _boltHubService.ConnectAsync();

        _boltHubCaller = new BoltClient(new Uri("ws://localhost:18100/bolt"),
            "bolt_caller", "BoltCaller", opts, lf.CreateLogger<BoltClient>());
        await _boltHubCaller.ConnectAsync();
    }

    private async Task SetupBoltDirect()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://localhost:18200");
        builder.Services.AddSingleton<BoltServer>();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        _boltDirectApp = builder.Build();
        _boltDirectApp.Services.GetRequiredService<BoltServer>().RegisterHandler("hello", HelloHandler);
        _boltDirectApp.UseWebSockets();
        _boltDirectApp.MapBolt("/bolt");
        _boltDirectApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _boltDirectApp.RunAsync());
        await WaitForHealth("http://localhost:18200/health");

        var lf = _boltDirectApp.Services.GetRequiredService<ILoggerFactory>();
        _boltDirectClient = new BoltClient(new Uri("ws://localhost:18200/bolt"),
            "direct_caller", "DirectCaller", new BoltClientOptions { RpcTimeoutSeconds = 30 },
            lf.CreateLogger<BoltClient>());
        await _boltDirectClient.ConnectAsync();
    }

    private async Task SetupGrpc()
    {
        // Backend
        var bb = WebApplication.CreateBuilder();
        bb.WebHost.ConfigureKestrel(o =>
        {
            o.ListenLocalhost(18301, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(18302, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        bb.Services.AddGrpc();
        bb.Logging.SetMinimumLevel(LogLevel.Error);
        _grpcBackendApp = bb.Build();
        _grpcBackendApp.MapGrpcService<GrpcHelloBackend>();
        _grpcBackendApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _grpcBackendApp.RunAsync());
        await WaitForHealth("http://localhost:18302/health");

        // Hub
        var backendChannel = GrpcChannel.ForAddress("http://localhost:18301");
        var backendClient = new HelloService.HelloServiceClient(backendChannel);
        var hb = WebApplication.CreateBuilder();
        hb.WebHost.ConfigureKestrel(o =>
        {
            o.ListenLocalhost(18303, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(18304, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        hb.Services.AddGrpc();
        hb.Services.AddSingleton(backendClient);
        hb.Logging.SetMinimumLevel(LogLevel.Error);
        _grpcHubApp = hb.Build();
        _grpcHubApp.MapGrpcService<GrpcHelloHub>();
        _grpcHubApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _grpcHubApp.RunAsync());
        await WaitForHealth("http://localhost:18304/health");

        // Client → Hub
        _grpcChannel = GrpcChannel.ForAddress("http://localhost:18303");
        _grpcClient = new HelloService.HelloServiceClient(_grpcChannel);
    }

    private async Task SetupGrpcDirect()
    {
        // Single gRPC server — client connects directly (no hub routing)
        var db = WebApplication.CreateBuilder();
        db.WebHost.ConfigureKestrel(o =>
        {
            o.ListenLocalhost(18305, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(18306, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        db.Services.AddGrpc();
        db.Logging.SetMinimumLevel(LogLevel.Error);
        _grpcDirectApp = db.Build();
        _grpcDirectApp.MapGrpcService<GrpcHelloBackend>();
        _grpcDirectApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _grpcDirectApp.RunAsync());
        await WaitForHealth("http://localhost:18306/health");

        _grpcDirectChannel = GrpcChannel.ForAddress("http://localhost:18305");
        _grpcDirectClient = new HelloService.HelloServiceClient(_grpcDirectChannel);
    }

    private async Task SetupSignalR()
    {
        // Backend — handles actual Hello logic
        var bb = WebApplication.CreateBuilder();
        bb.WebHost.UseUrls("http://localhost:18400");
        bb.Services.AddSignalR().AddMessagePackProtocol();
        bb.Logging.SetMinimumLevel(LogLevel.Error);
        _signalRBackendApp = bb.Build();
        _signalRBackendApp.MapHub<HelloBackendHub>("/hello-backend");
        _signalRBackendApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _signalRBackendApp.RunAsync());
        await WaitForHealth("http://localhost:18400/health");

        // Hub — proxies to backend (same routing as Bolt Hub and gRPC Hub)
        var backendConn = new HubConnectionBuilder()
            .WithUrl("http://localhost:18400/hello-backend")
            .AddMessagePackProtocol()
            .Build();
        await backendConn.StartAsync();

        var hb = WebApplication.CreateBuilder();
        hb.WebHost.UseUrls("http://localhost:18401");
        hb.Services.AddSignalR().AddMessagePackProtocol();
        hb.Services.AddSingleton(backendConn);
        hb.Logging.SetMinimumLevel(LogLevel.Error);
        _signalRHubApp = hb.Build();
        _signalRHubApp.MapHub<HelloRouterHub>("/hello-hub");
        _signalRHubApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _signalRHubApp.RunAsync());
        await WaitForHealth("http://localhost:18401/health");

        // Client → Hub (not backend)
        _signalRCaller = new HubConnectionBuilder()
            .WithUrl("http://localhost:18401/hello-hub")
            .AddMessagePackProtocol()
            .Build();
        await _signalRCaller.StartAsync();
    }

    #endregion

    #region Handlers

    private static Task<(HttpStatusCode, ReadOnlyMemory<byte>)> HelloHandler(ReadOnlyMemory<byte> payload, Guid requestId)
    {
        var req = MemoryPackSerializer.Deserialize<HelloMsg>(payload.Span)!;
        var resp = new HelloMsg { Text = $"Hello {req.Text}" };
        return Task.FromResult((HttpStatusCode.OK, (ReadOnlyMemory<byte>)MemoryPackSerializer.Serialize(resp)));
    }

    #endregion

    #region Call helpers

    private async Task BoltHubCall()
    {
        var payload = MemoryPackSerializer.Serialize(new HelloMsg { Text = "World" });
        await _boltHubCaller.InvokeAsync("bolt_service", "hello", payload);
    }

    private async Task BoltDirectCall()
    {
        var payload = MemoryPackSerializer.Serialize(new HelloMsg { Text = "World" });
        await _boltDirectClient.InvokeAsync("_", "hello", payload);
    }

    private async Task GrpcCall()
    {
        await _grpcClient.SayHelloAsync(new HelloRequest { Name = "World" });
    }

    private async Task GrpcDirectCall()
    {
        await _grpcDirectClient.SayHelloAsync(new HelloRequest { Name = "World" });
    }

    private async Task SignalRCall()
    {
        await _signalRCaller.InvokeAsync<string>("SayHello", "World");
    }

    #endregion

    #region Benchmarks

    [Benchmark]
    public async Task Bolt_Hub()
    {
        var tasks = new Task[Concurrency];
        for (int i = 0; i < Concurrency; i++) tasks[i] = BoltHubCall();
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task Bolt_Direct()
    {
        var tasks = new Task[Concurrency];
        for (int i = 0; i < Concurrency; i++) tasks[i] = BoltDirectCall();
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task GRPC_Hub()
    {
        var tasks = new Task[Concurrency];
        for (int i = 0; i < Concurrency; i++) tasks[i] = GrpcCall();
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task GRPC_Direct()
    {
        var tasks = new Task[Concurrency];
        for (int i = 0; i < Concurrency; i++) tasks[i] = GrpcDirectCall();
        await Task.WhenAll(tasks);
    }

    [Benchmark(Baseline = true)]
    public async Task SignalR_Hub()
    {
        var tasks = new Task[Concurrency];
        for (int i = 0; i < Concurrency; i++) tasks[i] = SignalRCall();
        await Task.WhenAll(tasks);
    }

    #endregion

    [GlobalCleanup]
    public async Task Cleanup()
    {
        try { await _boltHubCaller.DisposeAsync(); } catch { }
        try { await _boltHubService.DisposeAsync(); } catch { }
        try { await _boltDirectClient.DisposeAsync(); } catch { }
        try { await _signalRCaller.DisposeAsync(); } catch { }
        _grpcChannel?.Dispose();
        _grpcDirectChannel?.Dispose();
        try { await _grpcHubApp.StopAsync(); } catch { }
        try { await _grpcBackendApp.StopAsync(); } catch { }
        try { await _grpcDirectApp.StopAsync(); } catch { }
        try { await _signalRHubApp.StopAsync(); } catch { }
        try { await _signalRBackendApp.StopAsync(); } catch { }
        try { await _boltHubApp.StopAsync(); } catch { }
        try { await _boltDirectApp.StopAsync(); } catch { }
    }

    private static async Task WaitForHealth(string url, int timeoutSeconds = 15)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try { if ((await client.GetAsync(url)).IsSuccessStatusCode) return; } catch { }
            await Task.Delay(200);
        }
        throw new TimeoutException($"Service at {url} not healthy within {timeoutSeconds}s");
    }
}

// ── Shared types ──

[MemoryPackable]
public partial record HelloMsg
{
    public string Text { get; init; } = "";
}

// ── gRPC services ──

public class GrpcHelloBackend : HelloService.HelloServiceBase
{
    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        => Task.FromResult(new HelloReply { Message = $"Hello {request.Name}" });
}

public class GrpcHelloHub : HelloService.HelloServiceBase
{
    private readonly HelloService.HelloServiceClient _backend;
    public GrpcHelloHub(HelloService.HelloServiceClient backend) => _backend = backend;
    public override async Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        => await _backend.SayHelloAsync(request);
}

// ── SignalR hubs (backend + router — same hop count as Bolt Hub and gRPC Hub) ──

/// <summary>Backend — handles actual logic (like IdentityServer in Bolt)</summary>
public class HelloBackendHub : Microsoft.AspNetCore.SignalR.Hub
{
    public string SayHello(string name) => $"Hello {name}";
}

/// <summary>Router — proxies to backend (like BoltServer hub routing)</summary>
public class HelloRouterHub : Microsoft.AspNetCore.SignalR.Hub
{
    private readonly HubConnection _backend;
    public HelloRouterHub(HubConnection backend) => _backend = backend;
    public async Task<string> SayHello(string name) => await _backend.InvokeAsync<string>("SayHello", name);
}

// ── Max Throughput Benchmark ──

/// <summary>
/// Fires a batch of 100 requests at max parallelism via Task.WhenAll.
/// OperationsPerInvoke=100 gives BenchmarkDotNet the per-op latency.
/// Tests peak ops/sec each transport can sustain.
/// </summary>
[Config(typeof(BoltBenchConfig))]
[MemoryDiagnoser]
public class BoltThroughputBenchmarks
{
    private WebApplication _boltHubApp = null!;
    private BoltClient _boltHubService = null!;
    private BoltClient _boltHubCaller = null!;
    private WebApplication _boltDirectApp = null!;
    private BoltClient _boltDirectClient = null!;
    private WebApplication _grpcBackendApp = null!;
    private WebApplication _grpcHubApp = null!;
    private GrpcChannel _grpcChannel = null!;
    private HelloService.HelloServiceClient _grpcClient = null!;
    private WebApplication _grpcDirectApp = null!;
    private GrpcChannel _grpcDirectChannel = null!;
    private HelloService.HelloServiceClient _grpcDirectClient = null!;
    private WebApplication _signalRBackendApp = null!;
    private WebApplication _signalRHubApp = null!;
    private HubConnection _signalRCaller = null!;

    private const int Batch = 100;

    [GlobalSetup]
    public async Task Setup()
    {
        // ── Bolt Hub ──
        var b1 = WebApplication.CreateBuilder();
        b1.WebHost.UseUrls("http://localhost:18500");
        b1.Services.AddSingleton<BoltServer>();
        b1.Logging.SetMinimumLevel(LogLevel.Error);
        _boltHubApp = b1.Build();
        _boltHubApp.UseWebSockets();
        _boltHubApp.MapBolt("/bolt");
        _boltHubApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _boltHubApp.RunAsync());
        await WaitForHealth("http://localhost:18500/health");

        var lf = _boltHubApp.Services.GetRequiredService<ILoggerFactory>();
        var opts = new BoltClientOptions { RpcTimeoutSeconds = 60, MinConnections = 4, MaxConnections = 8, ScaleUpThreshold = 16 };
        _boltHubService = new BoltClient(new Uri("ws://localhost:18500/bolt"),
            "tp_service", "TpService", opts, lf.CreateLogger<BoltClient>());
        _boltHubService.RegisterHandler("hello", HelloHandler);
        await _boltHubService.ConnectAsync();
        _boltHubCaller = new BoltClient(new Uri("ws://localhost:18500/bolt"),
            "tp_caller", "TpCaller", opts, lf.CreateLogger<BoltClient>());
        await _boltHubCaller.ConnectAsync();

        // ── Bolt Direct ──
        var b2 = WebApplication.CreateBuilder();
        b2.WebHost.UseUrls("http://localhost:18600");
        b2.Services.AddSingleton<BoltServer>();
        b2.Logging.SetMinimumLevel(LogLevel.Error);
        _boltDirectApp = b2.Build();
        _boltDirectApp.Services.GetRequiredService<BoltServer>().RegisterHandler("hello", HelloHandler);
        _boltDirectApp.UseWebSockets();
        _boltDirectApp.MapBolt("/bolt");
        _boltDirectApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _boltDirectApp.RunAsync());
        await WaitForHealth("http://localhost:18600/health");
        _boltDirectClient = new BoltClient(new Uri("ws://localhost:18600/bolt"),
            "tp_direct", "TpDirect", opts, lf.CreateLogger<BoltClient>());
        await _boltDirectClient.ConnectAsync();

        // ── gRPC Hub ──
        var gb = WebApplication.CreateBuilder();
        gb.WebHost.ConfigureKestrel(o => {
            o.ListenLocalhost(18701, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(18702, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        gb.Services.AddGrpc();
        gb.Logging.SetMinimumLevel(LogLevel.Error);
        _grpcBackendApp = gb.Build();
        _grpcBackendApp.MapGrpcService<GrpcHelloBackend>();
        _grpcBackendApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _grpcBackendApp.RunAsync());
        await WaitForHealth("http://localhost:18702/health");

        var backendCh = GrpcChannel.ForAddress("http://localhost:18701");
        var backendCl = new HelloService.HelloServiceClient(backendCh);
        var gh = WebApplication.CreateBuilder();
        gh.WebHost.ConfigureKestrel(o => {
            o.ListenLocalhost(18703, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(18704, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        gh.Services.AddGrpc();
        gh.Services.AddSingleton(backendCl);
        gh.Logging.SetMinimumLevel(LogLevel.Error);
        _grpcHubApp = gh.Build();
        _grpcHubApp.MapGrpcService<GrpcHelloHub>();
        _grpcHubApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _grpcHubApp.RunAsync());
        await WaitForHealth("http://localhost:18704/health");
        _grpcChannel = GrpcChannel.ForAddress("http://localhost:18703");
        _grpcClient = new HelloService.HelloServiceClient(_grpcChannel);

        // ── gRPC Direct ──
        var gd = WebApplication.CreateBuilder();
        gd.WebHost.ConfigureKestrel(o => {
            o.ListenLocalhost(18705, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(18706, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        gd.Services.AddGrpc();
        gd.Logging.SetMinimumLevel(LogLevel.Error);
        _grpcDirectApp = gd.Build();
        _grpcDirectApp.MapGrpcService<GrpcHelloBackend>();
        _grpcDirectApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _grpcDirectApp.RunAsync());
        await WaitForHealth("http://localhost:18706/health");
        _grpcDirectChannel = GrpcChannel.ForAddress("http://localhost:18705");
        _grpcDirectClient = new HelloService.HelloServiceClient(_grpcDirectChannel);

        // ── SignalR Hub ──
        var sb = WebApplication.CreateBuilder();
        sb.WebHost.UseUrls("http://localhost:18800");
        sb.Services.AddSignalR().AddMessagePackProtocol();
        sb.Logging.SetMinimumLevel(LogLevel.Error);
        _signalRBackendApp = sb.Build();
        _signalRBackendApp.MapHub<HelloBackendHub>("/hello-backend");
        _signalRBackendApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _signalRBackendApp.RunAsync());
        await WaitForHealth("http://localhost:18800/health");

        var backendConn = new HubConnectionBuilder()
            .WithUrl("http://localhost:18800/hello-backend").AddMessagePackProtocol().Build();
        await backendConn.StartAsync();
        var sh = WebApplication.CreateBuilder();
        sh.WebHost.UseUrls("http://localhost:18801");
        sh.Services.AddSignalR().AddMessagePackProtocol();
        sh.Services.AddSingleton(backendConn);
        sh.Logging.SetMinimumLevel(LogLevel.Error);
        _signalRHubApp = sh.Build();
        _signalRHubApp.MapHub<HelloRouterHub>("/hello-hub");
        _signalRHubApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _signalRHubApp.RunAsync());
        await WaitForHealth("http://localhost:18801/health");
        _signalRCaller = new HubConnectionBuilder()
            .WithUrl("http://localhost:18801/hello-hub").AddMessagePackProtocol().Build();
        await _signalRCaller.StartAsync();

        // Warmup
        for (int i = 0; i < 10; i++)
        {
            var p = MemoryPackSerializer.Serialize(new HelloMsg { Text = "W" });
            await _boltHubCaller.InvokeAsync("tp_service", "hello", p);
            await _boltDirectClient.InvokeAsync("_", "hello", p);
            await _grpcClient.SayHelloAsync(new HelloRequest { Name = "W" });
            await _grpcDirectClient.SayHelloAsync(new HelloRequest { Name = "W" });
            await _signalRCaller.InvokeAsync<string>("SayHello", "W");
        }
    }

    private static Task<(HttpStatusCode, ReadOnlyMemory<byte>)> HelloHandler(ReadOnlyMemory<byte> payload, Guid requestId)
    {
        var req = MemoryPackSerializer.Deserialize<HelloMsg>(payload.Span)!;
        var resp = new HelloMsg { Text = $"Hello {req.Text}" };
        return Task.FromResult((HttpStatusCode.OK, (ReadOnlyMemory<byte>)MemoryPackSerializer.Serialize(resp)));
    }

    [Benchmark(OperationsPerInvoke = Batch)]
    public async Task Bolt_Hub_Throughput()
    {
        var tasks = new Task[Batch];
        for (int i = 0; i < Batch; i++)
        {
            var p = MemoryPackSerializer.Serialize(new HelloMsg { Text = "World" });
            tasks[i] = _boltHubCaller.InvokeAsync("tp_service", "hello", p);
        }
        await Task.WhenAll(tasks);
    }

    [Benchmark(OperationsPerInvoke = Batch)]
    public async Task Bolt_Direct_Throughput()
    {
        var tasks = new Task[Batch];
        for (int i = 0; i < Batch; i++)
        {
            var p = MemoryPackSerializer.Serialize(new HelloMsg { Text = "World" });
            tasks[i] = _boltDirectClient.InvokeAsync("_", "hello", p);
        }
        await Task.WhenAll(tasks);
    }

    [Benchmark(OperationsPerInvoke = Batch)]
    public async Task GRPC_Hub_Throughput()
    {
        var tasks = new Task[Batch];
        for (int i = 0; i < Batch; i++)
            tasks[i] = _grpcClient.SayHelloAsync(new HelloRequest { Name = "World" }).ResponseAsync;
        await Task.WhenAll(tasks);
    }

    [Benchmark(OperationsPerInvoke = Batch)]
    public async Task GRPC_Direct_Throughput()
    {
        var tasks = new Task[Batch];
        for (int i = 0; i < Batch; i++)
            tasks[i] = _grpcDirectClient.SayHelloAsync(new HelloRequest { Name = "World" }).ResponseAsync;
        await Task.WhenAll(tasks);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Batch)]
    public async Task SignalR_Hub_Throughput()
    {
        var tasks = new Task[Batch];
        for (int i = 0; i < Batch; i++)
            tasks[i] = _signalRCaller.InvokeAsync<string>("SayHello", "World");
        await Task.WhenAll(tasks);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        try { await _boltHubCaller.DisposeAsync(); } catch { }
        try { await _boltHubService.DisposeAsync(); } catch { }
        try { await _boltDirectClient.DisposeAsync(); } catch { }
        try { await _signalRCaller.DisposeAsync(); } catch { }
        _grpcChannel?.Dispose();
        _grpcDirectChannel?.Dispose();
        try { await _grpcHubApp.StopAsync(); } catch { }
        try { await _grpcBackendApp.StopAsync(); } catch { }
        try { await _grpcDirectApp.StopAsync(); } catch { }
        try { await _signalRHubApp.StopAsync(); } catch { }
        try { await _signalRBackendApp.StopAsync(); } catch { }
        try { await _boltHubApp.StopAsync(); } catch { }
        try { await _boltDirectApp.StopAsync(); } catch { }
    }

    private static async Task WaitForHealth(string url, int timeoutSeconds = 15)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try { if ((await client.GetAsync(url)).IsSuccessStatusCode) return; } catch { }
            await Task.Delay(200);
        }
        throw new TimeoutException($"Service at {url} not healthy within {timeoutSeconds}s");
    }
}

// ── Config ──

file class BoltBenchConfig : ManualConfig
{
    public BoltBenchConfig()
    {
        AddJob(Job.ShortRun.WithWarmupCount(3).WithIterationCount(10));
        AddColumn(StatisticColumn.P95);
        AddColumn(new BoltOpsPerSecColumn());
    }
}

file class BoltOpsPerSecColumn : IColumn
{
    public string Id => "OpsPerSec";
    public string ColumnName => "Op/s";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => 0;
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "Operations per second";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        => GetValue(summary, benchmarkCase, SummaryStyle.Default);

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        var report = summary[benchmarkCase];
        if (report?.ResultStatistics == null) return "N/A";
        return (1_000_000_000.0 / report.ResultStatistics.Mean).ToString("N0");
    }

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
    public bool IsAvailable(Summary summary) => true;
}
