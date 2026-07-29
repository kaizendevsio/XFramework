using System.Net;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Bolt.Client;
using Bolt.Server;
using Bolt.Tests.Grpc;
using Grpc.Net.Client;
using MemoryPack;
using Perfolizer.Horology;

namespace Bolt.Tests;

/// <summary>
/// In-process scalability benchmark with one connection/channel per caller.
///
/// Tests: 10, 50, 100 clients. Each fires 1 request simultaneously.
/// Measures batch completion and whole-process allocation regressions at scale.
/// </summary>
[Config(typeof(ScalabilityConfig))]
[MemoryDiagnoser]
public class ScalabilityBenchmarks
{
    // Bolt
    private WebApplication _boltApp = null!;
    private BoltClient _boltService = null!;
    private List<BoltClient> _boltClients = [];

    // gRPC
    private WebApplication _grpcBackendApp = null!;
    private WebApplication _grpcHubApp = null!;
    private GrpcChannel _grpcBackendChannel = null!;
    private List<HelloService.HelloServiceClient> _grpcClients = [];
    private List<GrpcChannel> _grpcChannels = [];

    [Params(10, 50, 100)]
    public int ClientCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        await SetupBolt();
        await SetupGrpc();

        // Warmup
        for (int i = 0; i < 5; i++)
        {
            var p = MemoryPackSerializer.Serialize(new HelloMsg { Text = "W" });
            await ValidateBoltHello(_boltClients[0].InvokeAsync("bolt_svc", "hello", p), "Hello W");
            await ValidateGrpcHello(_grpcClients[0].SayHelloAsync(new HelloRequest { Name = "W" }).ResponseAsync, "Hello W");
        }

        // Report connection counts
        var boltServer = _boltApp.Services.GetRequiredService<BoltServer>();
        Console.WriteLine($"[Setup] Bolt: {ClientCount} clients × 1 connection = {boltServer.ConnectedClients} hub connections (+ 1 service connection)");
        Console.WriteLine($"[Setup] gRPC: {ClientCount} clients × 1 channel each");
    }

    private async Task SetupBolt()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://localhost:18900");
        builder.Services.AddSingleton<BoltServer>();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        _boltApp = builder.Build();
        _boltApp.UseWebSockets();
        _boltApp.MapBolt("/bolt");
        _boltApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _boltApp.RunAsync());
        await WaitForHealth("http://localhost:18900/health");

        var lf = _boltApp.Services.GetRequiredService<ILoggerFactory>();

        // Service uses one connection, matching the single gRPC backend channel.
        var svcOpts = new BoltClientOptions { RpcTimeoutSeconds = 30, MinConnections = 1, MaxConnections = 1 };
        _boltService = new BoltClient(new Uri("ws://localhost:18900/bolt"),
            "bolt_svc", "BoltSvc", svcOpts, lf.CreateLogger<BoltClient>());
        _boltService.RegisterHandler("hello", HelloHandler);
        await _boltService.ConnectAsync();

        // Create N clients, each with one connection/channel.
        var clientOpts = new BoltClientOptions { RpcTimeoutSeconds = 30, MinConnections = 1, MaxConnections = 1 };
        _boltClients = [];
        for (int i = 0; i < ClientCount; i++)
        {
            var client = new BoltClient(new Uri("ws://localhost:18900/bolt"),
                $"client_{i}", $"Client{i}", clientOpts, lf.CreateLogger<BoltClient>());
            await client.ConnectAsync();
            _boltClients.Add(client);
        }
    }

    private async Task SetupGrpc()
    {
        // Backend
        var bb = WebApplication.CreateBuilder();
        bb.WebHost.ConfigureKestrel(o =>
        {
            o.ListenLocalhost(18901, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(18902, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        bb.Services.AddGrpc();
        bb.Logging.SetMinimumLevel(LogLevel.Error);
        _grpcBackendApp = bb.Build();
        _grpcBackendApp.MapGrpcService<GrpcHelloBackend>();
        _grpcBackendApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _grpcBackendApp.RunAsync());
        await WaitForHealth("http://localhost:18902/health");

        // Hub
        _grpcBackendChannel = GrpcChannel.ForAddress("http://localhost:18901");
        var backendCl = new HelloService.HelloServiceClient(_grpcBackendChannel);
        var hb = WebApplication.CreateBuilder();
        hb.WebHost.ConfigureKestrel(o =>
        {
            o.ListenLocalhost(18903, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(18904, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        hb.Services.AddGrpc();
        hb.Services.AddSingleton(backendCl);
        hb.Logging.SetMinimumLevel(LogLevel.Error);
        _grpcHubApp = hb.Build();
        _grpcHubApp.MapGrpcService<GrpcHelloHub>();
        _grpcHubApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _grpcHubApp.RunAsync());
        await WaitForHealth("http://localhost:18904/health");

        // Create N gRPC clients (each gets its own channel)
        _grpcClients = [];
        _grpcChannels = [];
        for (int i = 0; i < ClientCount; i++)
        {
            var ch = GrpcChannel.ForAddress("http://localhost:18903");
            _grpcChannels.Add(ch);
            _grpcClients.Add(new HelloService.HelloServiceClient(ch));
        }
    }

    private static Task<(HttpStatusCode, ReadOnlyMemory<byte>)> HelloHandler(ReadOnlyMemory<byte> payload, Guid requestId)
    {
        var req = MemoryPackSerializer.Deserialize<HelloMsg>(payload.Span)!;
        var resp = new HelloMsg { Text = $"Hello {req.Text}" };
        return Task.FromResult((HttpStatusCode.OK, (ReadOnlyMemory<byte>)MemoryPackSerializer.Serialize(resp)));
    }

    /// <summary>
    /// All N clients fire one request simultaneously.
    /// Measures how the transport handles many concurrent clients.
    /// </summary>
    [Benchmark]
    public async Task Bolt_Hub_AllClients()
    {
        var tasks = new Task[ClientCount];
        for (int i = 0; i < ClientCount; i++)
        {
            var client = _boltClients[i];
            var p = MemoryPackSerializer.Serialize(new HelloMsg { Text = "World" });
            tasks[i] = ValidateBoltHello(client.InvokeAsync("bolt_svc", "hello", p), "Hello World");
        }
        await Task.WhenAll(tasks);
    }

    [Benchmark(Baseline = true)]
    public async Task GRPC_Hub_AllClients()
    {
        var tasks = new Task[ClientCount];
        for (int i = 0; i < ClientCount; i++)
        {
            tasks[i] = ValidateGrpcHello(
                _grpcClients[i].SayHelloAsync(new HelloRequest { Name = "World" }).ResponseAsync,
                "Hello World");
        }
        await Task.WhenAll(tasks);
    }

    private static async Task ValidateBoltHello(
        Task<(HttpStatusCode StatusCode, ReadOnlyMemory<byte> Payload)> pending,
        string expected)
    {
        var response = await pending;
        if (response.StatusCode != HttpStatusCode.OK ||
            MemoryPackSerializer.Deserialize<HelloMsg>(response.Payload.Span)?.Text != expected)
            throw new InvalidOperationException("Bolt returned an invalid benchmark response.");
    }

    private static async Task ValidateGrpcHello(Task<HelloReply> pending, string expected)
    {
        if ((await pending).Message != expected)
            throw new InvalidOperationException("gRPC returned an invalid benchmark response.");
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        foreach (var c in _boltClients)
            try { await c.DisposeAsync(); } catch { }
        try { await _boltService.DisposeAsync(); } catch { }
        foreach (var ch in _grpcChannels)
            ch.Dispose();
        _grpcBackendChannel?.Dispose();
        try { await _grpcHubApp.StopAsync(); } catch { }
        try { await _grpcBackendApp.StopAsync(); } catch { }
        try { await _boltApp.StopAsync(); } catch { }
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

public class ScalabilityConfig : ManualConfig
{
    public ScalabilityConfig()
    {
        AddJob(Job.Default
            .WithLaunchCount(3)
            .WithWarmupCount(5)
            .WithIterationCount(15)
            .WithMinIterationTime(TimeInterval.FromMilliseconds(250)));
        AddColumn(StatisticColumn.P95);
    }
}
