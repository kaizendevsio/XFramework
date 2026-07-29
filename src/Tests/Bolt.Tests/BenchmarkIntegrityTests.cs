using System.Net;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
[NonParallelizable]
public class BenchmarkIntegrityTests
{
    [Test]
    public async Task DynamicLoopbackHosts_UseDistinctEphemeralPortsAndAreObserved()
    {
        await using var first = await StartTestHostAsync("first");
        await using var second = await StartTestHostAsync("second");

        using var client = new HttpClient();
        var firstResponse = await client.GetStringAsync(first.BaseAddress);
        var secondResponse = await client.GetStringAsync(second.BaseAddress);

        Assert.Multiple(() =>
        {
            Assert.That(first.BaseAddress.Host, Is.EqualTo(IPAddress.Loopback.ToString()));
            Assert.That(first.BaseAddress.Port, Is.GreaterThan(0));
            Assert.That(second.BaseAddress.Port, Is.Not.EqualTo(first.BaseAddress.Port));
            Assert.That(firstResponse, Is.EqualTo("ok"));
            Assert.That(secondResponse, Is.EqualTo("ok"));
            Assert.DoesNotThrow(first.ThrowIfExited);
            Assert.DoesNotThrow(second.ThrowIfExited);
        });
    }

    [Test]
    public void BenchmarkCallDeadline_ReportsServerFailureBeforeCallDeadline()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var operation = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously).Task;
        var serverFailure = Task.FromException(
            new InvalidOperationException("observed server failure"));

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await BenchmarkCallDeadline.AwaitAsync(
                operation,
                deadline,
                serverFailure,
                "test call"));

        Assert.That(exception!.Message, Does.Contain("observed server failure"));
    }

    [Test]
    public void BenchmarkCallDeadline_ClassifiesLogicalTimeout()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        var serverFailure = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously).Task;
        var cancellationIgnoringOperation = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously).Task;

        var exception = Assert.ThrowsAsync<TimeoutException>(async () =>
            await BenchmarkCallDeadline.AwaitAsync(
                cancellationIgnoringOperation,
                deadline,
                serverFailure,
                "test call"));

        Assert.That(exception!.Message, Does.Contain("logical benchmark deadline"));
    }

    [Test]
    public void LatencyStatistics_ReportRequestLevelNearestRankPercentiles()
    {
        var durations = Enumerable.Range(1, 100).Select(static value => (long)value).ToArray();

        var statistics = LatencyStatistics.Calculate(durations);
        var millisecondsPerTimestamp = 1_000d / System.Diagnostics.Stopwatch.Frequency;

        Assert.Multiple(() =>
        {
            Assert.That(statistics.P50Milliseconds, Is.EqualTo(50 * millisecondsPerTimestamp));
            Assert.That(statistics.P95Milliseconds, Is.EqualTo(95 * millisecondsPerTimestamp));
            Assert.That(statistics.P99Milliseconds, Is.EqualTo(99 * millisecondsPerTimestamp));
            Assert.That(statistics.MaxMilliseconds, Is.EqualTo(100 * millisecondsPerTimestamp));
        });
    }

    [Test]
    public void RandomizedBlocks_AreDeterministicAndContainEveryVariantPerRepetition()
    {
        string[] variants = ["baseline", "candidate"];

        var first = BenchmarkBlockRandomizer.Create(variants, 3, 42);
        var second = BenchmarkBlockRandomizer.Create(variants, 3, 42);

        Assert.That(first.Select(static block => block.Value),
            Is.EqualTo(second.Select(static block => block.Value)));
        foreach (var repetition in first.GroupBy(static block => block.Repetition))
            Assert.That(repetition.Select(static block => block.Value), Is.EquivalentTo(variants));
    }

    [Test]
    public void HeaderAlignedChunkSweep_IncludesAllPhysicalFrameBoundariesAndBaseline()
    {
        Assert.That(
            BenchmarkTuning.HeaderAlignedChunkSweepBytes,
            Is.EqualTo(new[] { 65_515, 131_051, 262_123, 262_144 }));
    }

    [Test]
    public void PayloadBenchmarkGroups_HaveExactlyOneBaselineAndNoCrossTopologyRatios()
    {
        Type[] groups =
        {
            typeof(PayloadBenchmarks),
            typeof(DirectPayloadBenchmarks),
            typeof(RoutedPayloadBenchmarks)
        };

        foreach (var group in groups)
        {
            var benchmarks = group.GetMethods()
                .SelectMany(method => method.GetCustomAttributes(typeof(BenchmarkAttribute), false)
                    .Cast<BenchmarkAttribute>())
                .ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(benchmarks, Has.Length.EqualTo(2), group.Name);
                Assert.That(benchmarks.Count(static benchmark => benchmark.Baseline), Is.EqualTo(1), group.Name);
            });
        }
    }

    [TestCase(typeof(DirectPayloadBenchmarks), nameof(DirectPayloadBenchmarks.Bolt_Direct_Echo), nameof(DirectPayloadBenchmarks.SetupBoltBenchmark))]
    [TestCase(typeof(DirectPayloadBenchmarks), nameof(DirectPayloadBenchmarks.GRPC_Direct_Echo), nameof(DirectPayloadBenchmarks.SetupGrpcBenchmark))]
    [TestCase(typeof(RoutedPayloadBenchmarks), nameof(RoutedPayloadBenchmarks.Bolt_Routed_Echo), nameof(RoutedPayloadBenchmarks.SetupBoltBenchmark))]
    [TestCase(typeof(RoutedPayloadBenchmarks), nameof(RoutedPayloadBenchmarks.GRPC_Routed_Echo), nameof(RoutedPayloadBenchmarks.SetupGrpcBenchmark))]
    public void MatchedTopologyBenchmarks_HaveIsolatedSetup(
        Type benchmarkType,
        string benchmarkName,
        string setupName)
    {
        var setup = benchmarkType.GetMethod(setupName)!;
        var attribute = setup.GetCustomAttributes(typeof(GlobalSetupAttribute), false)
            .Cast<GlobalSetupAttribute>()
            .Single();

        Assert.That(attribute.Targets, Is.EqualTo(new[] { benchmarkName }));
    }

    [Test]
    public void LatencyOptions_DefaultToAllTopologiesAndCredibleRequestCount()
    {
        var parsed = PayloadLatencyOptions.TryParse(["--latency"], out var options);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(options!.Worker, Is.False);
            Assert.That(options.Requests, Is.EqualTo(5_000));
            Assert.That(options.Concurrency, Is.EqualTo(500));
            Assert.That(options.Repetitions, Is.EqualTo(3));
            Assert.That(options.Topologies, Is.EquivalentTo(Enum.GetValues<PayloadBenchmarkTopology>()));
        });
    }

    [Test]
    public void ChunkSweep_DefaultsToExecutableHeaderAlignedVariants()
    {
        PayloadLatencyOptions.TryParse(["--latency", "--chunk-sweep"], out var options);

        Assert.Multiple(() =>
        {
            Assert.That(options!.ChunkSweep, Is.True);
            Assert.That(options.ChunkSizes, Is.EqualTo(BenchmarkTuning.HeaderAlignedChunkSweepBytes));
            Assert.That(options.Topologies.Count(static topology => topology.IsBolt()), Is.EqualTo(2));
        });
    }

    [Test]
    public void HubConnectionScaleOptions_DefaultToRequiredMatrix()
    {
        var parsed = HubConnectionScaleOptions.TryParse(["--hub-connection-scale"], out var options);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(options!.ClientCounts, Is.EqualTo(new[] { 100, 500, 1_000 }));
            Assert.That(options.HubReceiveBufferBytes, Is.EqualTo(new[] { 65_536, 131_072, 262_144 }));
            Assert.That(options.Workloads, Is.EquivalentTo(Enum.GetValues<HubConnectionWorkload>()));
            Assert.That(options.Worker, Is.False);
        });
    }

    [TestCase("--latency", "--unknown-latency-option")]
    [TestCase("--hub-connection-scale", "--unknown-hub-option")]
    public void HarnessOptions_RejectUnknownOptions(string mode, string unknownOption)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            if (mode == "--latency")
                PayloadLatencyOptions.TryParse([mode, unknownOption, "1"], out _);
            else
                HubConnectionScaleOptions.TryParse([mode, unknownOption, "1"], out _);
        });
    }

    [Test]
    public async Task GrpcEnvironment_RecordsHighStreamLimitAndSingleCallerChannel()
    {
        await using var environment = await PayloadBenchmarkEnvironment.CreateAsync(
            PayloadBenchmarkTopology.DirectGrpc,
            128,
            nameof(GrpcEnvironment_RecordsHighStreamLimitAndSingleCallerChannel));
        var settings = environment.GetEffectiveSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings["grpcHttp2MaxStreamsPerConnection"], Is.EqualTo(2_048));
            Assert.That(settings["grpcCallerChannelCount"], Is.EqualTo(1));
            Assert.That(settings["grpcMultipleHttp2ConnectionsEnabled"], Is.False);
        });
    }

    [Test]
    public async Task StartupTimeout_StopsAndDisposesApplication()
    {
        var builder = WebApplication.CreateBuilder();
        BenchmarkServerHost.ConfigureDynamicLoopback(builder, HttpProtocols.Http1);
        builder.Services.AddHostedService<BlockingStartupService>();
        builder.Logging.ClearProviders();
        var app = builder.Build();

        Assert.ThrowsAsync<TimeoutException>(async () =>
            await BenchmarkServerHost.StartAsync("timeout", app, TimeSpan.FromMilliseconds(25)));
    }

    [Test]
    public void ClosedLoopLimitation_IsExplicitInHarnessMetadata()
    {
        Assert.That(
            PayloadLatencyHarness.CoordinatedOmissionLimitation,
            Does.Contain("coordinated omission").IgnoreCase);
    }

    [Test]
    public async Task ContinuationA_BEnvironmentVariable_CanOverrideAndIsRecorded()
    {
        const string variable = "BOLT_BENCH_RUN_RPC_CONTINUATIONS_ASYNCHRONOUSLY";
        var previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            Assert.That(new Bolt.Client.BoltClientOptions().RunRpcContinuationsAsynchronously, Is.True);
            Environment.SetEnvironmentVariable(variable, "false");
            await using var environment = await PayloadBenchmarkEnvironment.CreateAsync(
                PayloadBenchmarkTopology.DirectBolt,
                128,
                nameof(ContinuationA_BEnvironmentVariable_CanOverrideAndIsRecorded));
            Assert.That(environment.GetEffectiveSettings()["runRpcContinuationsAsynchronously"], Is.False);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Test]
    public void ArtifactRecorder_WritesEffectiveSettingsAndEnvironment()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"bolt-benchmark-{Guid.NewGuid():N}");
        const string variable = "BOLT_BENCH_HUB_RECEIVE_BUFFER_BYTES";
        var previous = Environment.GetEnvironmentVariable(variable);

        try
        {
            Environment.SetEnvironmentVariable(variable, "131072");
            var path = BenchmarkArtifactRecorder.Record(
                "test",
                new Dictionary<string, object?> { ["hubReceiveBufferBytes"] = 131_072 },
                directory);
            using var document = JsonDocument.Parse(File.ReadAllText(path));

            Assert.Multiple(() =>
            {
                Assert.That(document.RootElement.GetProperty("runtime").GetString(), Is.Not.Empty);
                Assert.That(
                    document.RootElement.GetProperty("effectiveSettings")
                        .GetProperty("hubReceiveBufferBytes").GetInt32(),
                    Is.EqualTo(131_072));
                Assert.That(
                    document.RootElement.GetProperty("environment")
                        .GetProperty(variable).GetString(),
                    Is.EqualTo("131072"));
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<BenchmarkServerHost> StartTestHostAsync(string name)
    {
        var builder = WebApplication.CreateBuilder();
        BenchmarkServerHost.ConfigureDynamicLoopback(builder, HttpProtocols.Http1);
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.MapGet("/", () => "ok");
        return await BenchmarkServerHost.StartAsync(name, app);
    }

    private sealed class BlockingStartupService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

}
