using System.Buffers;
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
/// Standalone Bolt benchmarks — no XFramework dependencies.
/// Tests hub mode, direct mode, and concurrent load.
/// </summary>
[Config(typeof(BoltBenchConfig))]
[MemoryDiagnoser]
public class BoltBenchmarks
{
    // Hub mode
    private WebApplication _hubApp = null!;
    private BoltClient _hubServiceClient = null!;
    private BoltClient _hubCallerClient = null!;

    // Direct mode (client → server, no hub routing)
    private WebApplication _directApp = null!;
    private BoltClient _directClient = null!;

    private const string HubUrl = "http://localhost:18100";
    private const string DirectUrl = "http://localhost:18200";

    [Params(1, 64)]
    public int Concurrency { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        await SetupHub();
        await SetupDirect();

        // Warmup both paths
        for (int i = 0; i < 10; i++)
        {
            await InvokePing(_hubCallerClient, "hub_service");
            await InvokePingDirect(_directClient);
        }
    }

    private async Task SetupHub()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(HubUrl);
        builder.Services.AddSingleton<BoltServer>();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        _hubApp = builder.Build();
        _hubApp.UseWebSockets();
        _hubApp.MapBolt("/bolt");
        _hubApp.MapGet("/health", () => Results.Ok("ok"));
        _ = Task.Run(() => _hubApp.RunAsync());
        await WaitForHealth($"{HubUrl}/health");

        var lf = _hubApp.Services.GetRequiredService<ILoggerFactory>();
        var opts = new BoltClientOptions { RpcTimeoutSeconds = 30 };

        // Service client — handles pings
        _hubServiceClient = new BoltClient(
            new Uri($"ws://localhost:18100/bolt"), "hub_service", "HubService",
            opts, lf.CreateLogger<BoltClient>());
        _hubServiceClient.RegisterHandler("ping", PingHandler);
        await _hubServiceClient.ConnectAsync();

        // Caller client — sends pings
        _hubCallerClient = new BoltClient(
            new Uri($"ws://localhost:18100/bolt"), "hub_caller", "HubCaller",
            opts, lf.CreateLogger<BoltClient>());
        await _hubCallerClient.ConnectAsync();
    }

    private async Task SetupDirect()
    {
        // Direct server — handles requests locally, no routing
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(DirectUrl);
        builder.Services.AddSingleton<BoltServer>();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        _directApp = builder.Build();

        // Register handler directly on the server (direct mode)
        var server = _directApp.Services.GetRequiredService<BoltServer>();
        server.RegisterHandler("ping", PingHandler);

        _directApp.UseWebSockets();
        _directApp.MapBolt("/bolt");
        _directApp.MapGet("/health", () => Results.Ok("ok"));
        _ = Task.Run(() => _directApp.RunAsync());
        await WaitForHealth($"{DirectUrl}/health");

        var lf = _directApp.Services.GetRequiredService<ILoggerFactory>();
        var opts = new BoltClientOptions { RpcTimeoutSeconds = 30 };

        // Client connects directly to the server (which handles requests itself)
        _directClient = new BoltClient(
            new Uri($"ws://localhost:18200/bolt"), "direct_caller", "DirectCaller",
            opts, lf.CreateLogger<BoltClient>());
        await _directClient.ConnectAsync();
    }

    // Simple ping handler — returns a small response
    private static Task<(HttpStatusCode, ReadOnlyMemory<byte>)> PingHandler(ReadOnlyMemory<byte> payload, Guid requestId)
    {
        var response = new PingResponse { Message = "pong", Timestamp = DateTime.UtcNow.Ticks };
        var bytes = MemoryPackSerializer.Serialize(response);
        return Task.FromResult((HttpStatusCode.OK, (ReadOnlyMemory<byte>)bytes));
    }

    private static async Task<(HttpStatusCode, ReadOnlyMemory<byte>)> InvokePing(BoltClient client, string recipient)
    {
        var request = new PingRequest { Message = "ping" };
        var payload = MemoryPackSerializer.Serialize(request);
        return await client.InvokeAsync(recipient, "ping", payload);
    }

    private static async Task<(HttpStatusCode, ReadOnlyMemory<byte>)> InvokePingDirect(BoltClient client)
    {
        var request = new PingRequest { Message = "ping" };
        var payload = MemoryPackSerializer.Serialize(request);
        // recipientId doesn't matter in direct mode — server handles locally
        return await client.InvokeAsync("_", "ping", payload);
    }

    [Benchmark(Baseline = true)]
    public async Task Hub_Mode()
    {
        var tasks = new Task[Concurrency];
        for (int i = 0; i < Concurrency; i++)
            tasks[i] = InvokePing(_hubCallerClient, "hub_service");
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task Direct_Mode()
    {
        var tasks = new Task[Concurrency];
        for (int i = 0; i < Concurrency; i++)
            tasks[i] = InvokePingDirect(_directClient);
        await Task.WhenAll(tasks);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        try { await _hubCallerClient.DisposeAsync(); } catch { }
        try { await _hubServiceClient.DisposeAsync(); } catch { }
        try { await _directClient.DisposeAsync(); } catch { }
        try { await _hubApp.StopAsync(); } catch { }
        try { await _directApp.StopAsync(); } catch { }
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

[MemoryPackable]
public partial record PingRequest
{
    public string Message { get; init; } = "";
}

[MemoryPackable]
public partial record PingResponse
{
    public string Message { get; init; } = "";
    public long Timestamp { get; init; }
}

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
