using System.Net;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using Bolt.Client;
using Bolt.Server;
using Bolt.Tests.Grpc;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using MemoryPack;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Perfolizer.Horology;

namespace Bolt.Tests;

/// <summary>
/// Head-to-head payload size benchmark: Bolt vs gRPC.
/// Both transports echo raw bytes of varying sizes.
///
/// Tests answer: "How do Bolt and gRPC compare as payloads grow?"
///
/// Payload sizes:
/// - 100B:   tiny (metadata, status codes)
/// - 1KB:    typical chat message
/// - 32KB:   small file, JSON response
/// - 128KB:  medium (below Bolt auto-stream threshold)
/// - 512KB:  large (above 256KB → Bolt auto-streams via BoltStream)
/// </summary>
[Config(typeof(PayloadBenchConfig))]
[MemoryDiagnoser]
public class PayloadBenchmarks
{
    // Bolt
    private WebApplication _boltApp = null!;
    private BoltClient _boltService = null!;
    private BoltClient _boltCaller = null!;
    private byte[] _boltPayload = null!;

    // gRPC
    private WebApplication _grpcApp = null!;
    private GrpcChannel _grpcChannel = null!;
    private HelloService.HelloServiceClient _grpcClient = null!;
    private PayloadRequest _grpcRequest = null!;

    [Params(100, 1024, 32_768, 131_072, 524_288, 1_048_576, 2_097_152, 5_242_880, 10_485_760, 20_971_520)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        await SetupBolt();
        await SetupGrpc();
        GeneratePayloads();

        // Warmup both
        for (int i = 0; i < 10; i++)
        {
            await _boltCaller.InvokeAsync("payload_svc", "echo", _boltPayload);
            await _grpcClient.EchoPayloadAsync(_grpcRequest);
        }
    }

    private async Task SetupBolt()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://localhost:18600");
        builder.Services.AddSingleton<BoltServer>();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        _boltApp = builder.Build();
        _boltApp.UseWebSockets();
        _boltApp.MapBolt("/bolt");
        _boltApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _boltApp.RunAsync());
        await WaitForHealth("http://localhost:18600/health");

        var lf = _boltApp.Services.GetRequiredService<ILoggerFactory>();
        var opts = new BoltClientOptions { RpcTimeoutSeconds = 60 }; // Uses default 1MB threshold

        _boltService = new BoltClient(new Uri("ws://localhost:18600/bolt"),
            "payload_svc", "PayloadSvc", opts, lf.CreateLogger<BoltClient>());
        _boltService.RegisterHandler("echo", (payload, _) =>
            Task.FromResult((HttpStatusCode.OK, payload)));
        await _boltService.ConnectAsync();

        _boltCaller = new BoltClient(new Uri("ws://localhost:18600/bolt"),
            "payload_caller", "PayloadCaller", opts, lf.CreateLogger<BoltClient>());
        await _boltCaller.ConnectAsync();
    }

    private async Task SetupGrpc()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o =>
        {
            o.ListenLocalhost(18601, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(18602, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        builder.Services.AddGrpc(o => o.MaxReceiveMessageSize = 64 * 1024 * 1024);
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        _grpcApp = builder.Build();
        _grpcApp.MapGrpcService<GrpcEchoPayloadBackend>();
        _grpcApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _grpcApp.RunAsync());
        await WaitForHealth("http://localhost:18602/health");

        _grpcChannel = GrpcChannel.ForAddress("http://localhost:18601", new GrpcChannelOptions
        {
            MaxReceiveMessageSize = 64 * 1024 * 1024,
            MaxSendMessageSize = 64 * 1024 * 1024
        });
        _grpcClient = new HelloService.HelloServiceClient(_grpcChannel);
    }

    private void GeneratePayloads()
    {
        var data = new byte[PayloadBytes];
        Random.Shared.NextBytes(data);

        _boltPayload = MemoryPackSerializer.Serialize(new BenchPayload { Data = data });
        _grpcRequest = new PayloadRequest { Data = ByteString.CopyFrom(data) };
    }

    [IterationSetup]
    public void IterationSetup() => GeneratePayloads();

    [Benchmark(Baseline = true)]
    public async Task<(HttpStatusCode, ReadOnlyMemory<byte>)> Bolt_Echo()
    {
        return await _boltCaller.InvokeAsync("payload_svc", "echo", _boltPayload);
    }

    [Benchmark]
    public async Task<PayloadReply> GRPC_Echo()
    {
        return await _grpcClient.EchoPayloadAsync(_grpcRequest);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        try { await _boltCaller.DisposeAsync(); } catch { }
        try { await _boltService.DisposeAsync(); } catch { }
        _grpcChannel?.Dispose();
        try { await _grpcApp.StopAsync(); } catch { }
        try { await _boltApp.StopAsync(); } catch { }
    }

    private static async Task WaitForHealth(string url, int timeoutSeconds = 15)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try { if ((await client.GetAsync(url)).IsSuccessStatusCode) return; } catch { }
            await Task.Delay(100);
        }
        throw new TimeoutException($"Service at {url} not healthy within {timeoutSeconds}s");
    }
}

/// <summary>gRPC echo backend — returns the same byte payload.</summary>
public class GrpcEchoPayloadBackend : HelloService.HelloServiceBase
{
    public override Task<PayloadReply> EchoPayload(PayloadRequest request, ServerCallContext context)
    {
        return Task.FromResult(new PayloadReply { Data = request.Data });
    }

    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        return Task.FromResult(new HelloReply { Message = $"Hello {request.Name}" });
    }
}

[MemoryPackable]
public partial record BenchPayload
{
    public byte[] Data { get; init; } = [];
}

public class PayloadBenchConfig : ManualConfig
{
    public PayloadBenchConfig()
    {
        AddJob(Job.ShortRun.WithWarmupCount(3).WithIterationCount(10));
        AddColumn(StatisticColumn.P95);
        WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Microsecond));
    }
}
