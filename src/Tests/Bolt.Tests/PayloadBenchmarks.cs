using System.Net;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Bolt.Client;
using Bolt.Server;
using MemoryPack;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Perfolizer.Horology;

namespace Bolt.Tests;

/// <summary>
/// Payload size benchmark — measures Bolt RPC performance across different payload sizes.
/// Verifies no regression from protocol changes (senderHash addition, auto-streaming).
///
/// Payload sizes:
/// - 100B: tiny RPC (metadata, status)
/// - 1KB: typical chat message
/// - 32KB: small file, JSON response
/// - 128KB: medium payload (below auto-stream threshold)
/// - 512KB: large payload (above 256KB threshold → auto-streamed)
/// </summary>
[Config(typeof(PayloadBenchConfig))]
[MemoryDiagnoser]
public class PayloadBenchmarks
{
    private WebApplication _hubApp = null!;
    private BoltClient _service = null!;
    private BoltClient _caller = null!;
    private byte[] _payload = null!;

    [Params(100, 1024, 32768, 131072, 524288)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://localhost:18500");
        builder.Services.AddSingleton<BoltServer>();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        _hubApp = builder.Build();
        _hubApp.UseWebSockets();
        _hubApp.MapBolt("/bolt");
        _hubApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _hubApp.RunAsync());
        await WaitForHealth("http://localhost:18500/health");

        var loggerFactory = _hubApp.Services.GetRequiredService<ILoggerFactory>();
        var opts = new BoltClientOptions
        {
            RpcTimeoutSeconds = 60,
            LargePayloadThreshold = 262144, // 256KB default
        };

        _service = new BoltClient(new Uri("ws://localhost:18500/bolt"),
            "bench_svc", "BenchSvc", opts, loggerFactory.CreateLogger<BoltClient>());
        _service.RegisterHandler("echo", (payload, _) =>
            Task.FromResult((HttpStatusCode.OK, payload)));
        await _service.ConnectAsync();

        _caller = new BoltClient(new Uri("ws://localhost:18500/bolt"),
            "bench_caller", "BenchCaller", opts, loggerFactory.CreateLogger<BoltClient>());
        await _caller.ConnectAsync();

        // Generate payload
        var data = new byte[PayloadBytes];
        Random.Shared.NextBytes(data);
        _payload = MemoryPackSerializer.Serialize(new BenchPayload { Data = data });

        // Warmup
        for (int i = 0; i < 10; i++)
            await _caller.InvokeAsync("bench_svc", "echo", _payload);
    }

    [Benchmark]
    public async Task<(HttpStatusCode, ReadOnlyMemory<byte>)> Bolt_Echo()
    {
        return await _caller.InvokeAsync("bench_svc", "echo", _payload);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        try { await _caller.DisposeAsync(); } catch { }
        try { await _service.DisposeAsync(); } catch { }
        try { await _hubApp.StopAsync(); } catch { }
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
