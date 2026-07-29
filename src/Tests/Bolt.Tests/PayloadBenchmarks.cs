using System.Net;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using Bolt.Tests.Grpc;
using Google.Protobuf;
using Grpc.Core;
using MemoryPack;
using Perfolizer.Horology;

namespace Bolt.Tests;

/// <summary>
/// Existing product comparison: routed Bolt versus direct gRPC.
/// BenchmarkDotNet reports means, confidence intervals, and allocations. Request-level
/// percentiles are produced by the dedicated latency harness.
/// </summary>
[Config(typeof(PayloadBenchConfig))]
[MemoryDiagnoser]
public class PayloadBenchmarks
{
    private PayloadBenchmarkEnvironment _environment = null!;

    [ParamsSource(nameof(PayloadSizes))]
    public int PayloadBytes { get; set; }

    public static IEnumerable<int> PayloadSizes => BenchmarkTuning.GetPositiveValues(
        "BOLT_BENCH_PAYLOAD_BYTES",
        [100, 1024, 32_768, 131_072, 524_288, 1_048_576, 2_097_152, 5_242_880,
            10_485_760, 20_971_520]);

    [GlobalSetup(Target = nameof(Bolt_Echo))]
    public Task SetupBoltBenchmark() =>
        SetupAsync(PayloadBenchmarkTopology.RoutedBolt, nameof(Bolt_Echo));

    [GlobalSetup(Target = nameof(GRPC_Echo))]
    public Task SetupGrpcBenchmark() =>
        SetupAsync(PayloadBenchmarkTopology.DirectGrpc, nameof(GRPC_Echo));

    private async Task SetupAsync(PayloadBenchmarkTopology topology, string benchmarkName)
    {
        try
        {
            _environment = await PayloadBenchmarkEnvironment.CreateAsync(
                topology,
                PayloadBytes,
                $"PayloadBenchmarks.{benchmarkName}");

            Console.WriteLine(
                "Payload topology={0} input={1:N0} encoded={2:N0} threshold={3:N0} mode={4}",
                topology.Label(),
                PayloadBytes,
                topology.IsBolt() ? GetEncodedBoltPayloadSize(PayloadBytes) : PayloadBytes,
                _environment.BoltLargePayloadThreshold,
                topology.IsBolt() && GetEncodedBoltPayloadSize(PayloadBytes) > _environment.BoltLargePayloadThreshold
                    ? "stream"
                    : "unary");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"{topology.Label()} preflight failed for the {PayloadBytes:N0}-byte payload.",
                exception);
        }
    }

    [Benchmark(Baseline = true, Description = "Product_Routed_Bolt")]
    [BenchmarkCategory("ProductComparison", "Routed")]
    public Task<(HttpStatusCode StatusCode, ReadOnlyMemory<byte> Payload)> Bolt_Echo() =>
        _environment.InvokeBoltAsync();

    [Benchmark(Description = "Product_Direct_gRPC")]
    [BenchmarkCategory("ProductComparison", "Direct")]
    public Task<PayloadReply> GRPC_Echo() =>
        _environment.InvokeGrpcAsync();

    [GlobalCleanup(Targets =
    [
        nameof(Bolt_Echo),
        nameof(GRPC_Echo)
    ])]
    public async Task CleanupBenchmark()
    {
        if (_environment is not null)
            await _environment.DisposeAsync();
    }

    private static int GetEncodedBoltPayloadSize(int payloadBytes) =>
        MemoryPackSerializer.Serialize(new BenchPayload { Data = new byte[payloadBytes] }).Length;
}

/// <summary>Matched direct transport comparison with a direct-Bolt baseline.</summary>
[Config(typeof(PayloadBenchConfig))]
[MemoryDiagnoser]
public class DirectPayloadBenchmarks
{
    private PayloadBenchmarkEnvironment _environment = null!;

    [ParamsSource(nameof(PayloadSizes))]
    public int PayloadBytes { get; set; }

    public static IEnumerable<int> PayloadSizes => PayloadBenchmarks.PayloadSizes;

    [GlobalSetup(Target = nameof(Bolt_Direct_Echo))]
    public Task SetupBoltBenchmark() => SetupAsync(PayloadBenchmarkTopology.DirectBolt, nameof(Bolt_Direct_Echo));

    [GlobalSetup(Target = nameof(GRPC_Direct_Echo))]
    public Task SetupGrpcBenchmark() => SetupAsync(PayloadBenchmarkTopology.DirectGrpc, nameof(GRPC_Direct_Echo));

    private async Task SetupAsync(PayloadBenchmarkTopology topology, string benchmarkName) =>
        _environment = await PayloadBenchmarkEnvironment.CreateAsync(
            topology,
            PayloadBytes,
            $"DirectPayloadBenchmarks.{benchmarkName}");

    [Benchmark(Baseline = true, Description = "Direct_Bolt")]
    public Task<(HttpStatusCode StatusCode, ReadOnlyMemory<byte> Payload)> Bolt_Direct_Echo() =>
        _environment.InvokeBoltAsync();

    [Benchmark(Description = "Direct_gRPC")]
    public Task<PayloadReply> GRPC_Direct_Echo() => _environment.InvokeGrpcAsync();

    [GlobalCleanup(Targets = [nameof(Bolt_Direct_Echo), nameof(GRPC_Direct_Echo)])]
    public async Task CleanupBenchmark()
    {
        if (_environment is not null)
            await _environment.DisposeAsync();
    }
}

/// <summary>Matched routed transport comparison with a routed-Bolt baseline.</summary>
[Config(typeof(PayloadBenchConfig))]
[MemoryDiagnoser]
public class RoutedPayloadBenchmarks
{
    private PayloadBenchmarkEnvironment _environment = null!;

    [ParamsSource(nameof(PayloadSizes))]
    public int PayloadBytes { get; set; }

    public static IEnumerable<int> PayloadSizes => PayloadBenchmarks.PayloadSizes;

    [GlobalSetup(Target = nameof(Bolt_Routed_Echo))]
    public Task SetupBoltBenchmark() => SetupAsync(PayloadBenchmarkTopology.RoutedBolt, nameof(Bolt_Routed_Echo));

    [GlobalSetup(Target = nameof(GRPC_Routed_Echo))]
    public Task SetupGrpcBenchmark() => SetupAsync(PayloadBenchmarkTopology.RoutedGrpc, nameof(GRPC_Routed_Echo));

    private async Task SetupAsync(PayloadBenchmarkTopology topology, string benchmarkName) =>
        _environment = await PayloadBenchmarkEnvironment.CreateAsync(
            topology,
            PayloadBytes,
            $"RoutedPayloadBenchmarks.{benchmarkName}");

    [Benchmark(Baseline = true, Description = "Routed_Bolt")]
    public Task<(HttpStatusCode StatusCode, ReadOnlyMemory<byte> Payload)> Bolt_Routed_Echo() =>
        _environment.InvokeBoltAsync();

    [Benchmark(Description = "Routed_gRPC")]
    public Task<PayloadReply> GRPC_Routed_Echo() => _environment.InvokeGrpcAsync();

    [GlobalCleanup(Targets = [nameof(Bolt_Routed_Echo), nameof(GRPC_Routed_Echo)])]
    public async Task CleanupBenchmark()
    {
        if (_environment is not null)
            await _environment.DisposeAsync();
    }
}

/// <summary>gRPC echo backend returns the same byte payload.</summary>
public class GrpcEchoPayloadBackend : HelloService.HelloServiceBase
{
    public override Task<PayloadReply> EchoPayload(PayloadRequest request, ServerCallContext context) =>
        Task.FromResult(new PayloadReply { Data = request.Data });

    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context) =>
        Task.FromResult(new HelloReply { Message = $"Hello {request.Name}" });
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
        if (BenchmarkTuning.GetBoolean("BOLT_BENCH_QUICK", false))
        {
            AddJob(Job.Dry.WithId("PayloadSmoke"));
            WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Microsecond));
            return;
        }

        AddJob(Job.Default
            .WithId("PayloadCredible")
            .WithLaunchCount(3)
            .WithWarmupCount(5)
            .WithIterationCount(15)
            .WithMinIterationTime(TimeInterval.FromMilliseconds(250)));
        WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Microsecond));
    }
}

/// <summary>
/// Sustained product-path throughput comparison: routed Bolt versus direct gRPC.
/// Matched topology payload costs are measured by <see cref="PayloadBenchmarks"/>.
/// </summary>
[Config(typeof(ThroughputBenchConfig))]
[MemoryDiagnoser]
public class ThroughputBenchmarks
{
    private const int OpsPerInvocation = 100;
    private PayloadBenchmarkEnvironment _boltEnvironment = null!;
    private PayloadBenchmarkEnvironment _grpcEnvironment = null!;

    [Params(1024, 65_536)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _boltEnvironment = await PayloadBenchmarkEnvironment.CreateAsync(
            PayloadBenchmarkTopology.RoutedBolt,
            PayloadBytes,
            "ThroughputBenchmarks.Product_Routed_Bolt");
        _grpcEnvironment = await PayloadBenchmarkEnvironment.CreateAsync(
            PayloadBenchmarkTopology.DirectGrpc,
            PayloadBytes,
            "ThroughputBenchmarks.Product_Direct_gRPC");

        for (var index = 0; index < 10; index++)
        {
            await _boltEnvironment.InvokeBoltAsync();
            await _grpcEnvironment.InvokeGrpcAsync();
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = OpsPerInvocation)]
    public async Task Bolt_WebSocket_Throughput()
    {
        for (var index = 0; index < OpsPerInvocation; index++)
            await _boltEnvironment.InvokeBoltAsync();
    }

    [Benchmark(OperationsPerInvoke = OpsPerInvocation)]
    public async Task GRPC_Throughput()
    {
        for (var index = 0; index < OpsPerInvocation; index++)
            await _grpcEnvironment.InvokeGrpcAsync();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (_grpcEnvironment is not null)
            await _grpcEnvironment.DisposeAsync();
        if (_boltEnvironment is not null)
            await _boltEnvironment.DisposeAsync();
    }
}

public class ThroughputBenchConfig : ManualConfig
{
    public ThroughputBenchConfig()
    {
        AddJob(Job.Default
            .WithLaunchCount(3)
            .WithWarmupCount(5)
            .WithIterationCount(15)
            .WithMinIterationTime(TimeInterval.FromMilliseconds(250)));
        AddColumn(StatisticColumn.OperationsPerSecond);
        WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Microsecond));
    }
}

/// <summary>
/// Concurrent product-path batch benchmark. Batch completion is not request-level p95;
/// use the latency harness for p50, p95, p99, and maximum request latency.
/// </summary>
[Config(typeof(ConcurrencyBenchConfig))]
[MemoryDiagnoser]
public class ConcurrencyBenchmarks
{
    private const int PayloadSize = 1024;
    private PayloadBenchmarkEnvironment _boltEnvironment = null!;
    private PayloadBenchmarkEnvironment _grpcEnvironment = null!;

    [ParamsSource(nameof(ConcurrencyLevels))]
    public int Concurrency { get; set; }

    public static IEnumerable<int> ConcurrencyLevels => BenchmarkTuning.GetPositiveValues(
        "BOLT_BENCH_CONCURRENCY",
        [10, 50, 100, 500]);

    [GlobalSetup]
    public async Task Setup()
    {
        _boltEnvironment = await PayloadBenchmarkEnvironment.CreateAsync(
            PayloadBenchmarkTopology.RoutedBolt,
            PayloadSize,
            "ConcurrencyBenchmarks.Product_Routed_Bolt");
        _grpcEnvironment = await PayloadBenchmarkEnvironment.CreateAsync(
            PayloadBenchmarkTopology.DirectGrpc,
            PayloadSize,
            "ConcurrencyBenchmarks.Product_Direct_gRPC");

        await RunConcurrentBoltAsync();
        await RunConcurrentGrpcAsync();
    }

    [Benchmark(Baseline = true)]
    public Task Bolt_WebSocket_Concurrent() => RunConcurrentBoltAsync();

    [Benchmark]
    public Task GRPC_Concurrent() => RunConcurrentGrpcAsync();

    private async Task RunConcurrentBoltAsync()
    {
        var tasks = new Task[Concurrency];
        for (var index = 0; index < tasks.Length; index++)
            tasks[index] = _boltEnvironment.InvokeBoltAsync();
        await Task.WhenAll(tasks);
    }

    private async Task RunConcurrentGrpcAsync()
    {
        var tasks = new Task[Concurrency];
        for (var index = 0; index < tasks.Length; index++)
            tasks[index] = _grpcEnvironment.InvokeGrpcAsync();
        await Task.WhenAll(tasks);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (_grpcEnvironment is not null)
            await _grpcEnvironment.DisposeAsync();
        if (_boltEnvironment is not null)
            await _boltEnvironment.DisposeAsync();
    }
}

public class ConcurrencyBenchConfig : ManualConfig
{
    public ConcurrencyBenchConfig()
    {
        AddJob(Job.Default
            .WithLaunchCount(3)
            .WithWarmupCount(5)
            .WithIterationCount(15)
            .WithMinIterationTime(TimeInterval.FromMilliseconds(250)));
        WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Millisecond));
    }
}

internal static class BenchmarkResponseValidation
{
    public static void ValidateBoltPayload(
        (HttpStatusCode StatusCode, ReadOnlyMemory<byte> Payload) response,
        ReadOnlySpan<byte> expected)
    {
        if (response.StatusCode != HttpStatusCode.OK || !response.Payload.Span.SequenceEqual(expected))
            throw new InvalidOperationException("Bolt returned an invalid benchmark payload.");
    }

    public static void ValidateGrpcPayload(PayloadReply response, ByteString expected)
    {
        if (!response.Data.Span.SequenceEqual(expected.Span))
            throw new InvalidOperationException("gRPC returned an invalid benchmark payload.");
    }
}
