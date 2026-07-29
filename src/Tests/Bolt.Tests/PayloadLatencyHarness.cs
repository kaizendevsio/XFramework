using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bolt.Tests;

internal sealed record PayloadLatencyOptions(
    bool Worker,
    int PayloadBytes,
    int Concurrency,
    int Requests,
    int WarmupRequests,
    int Repetitions,
    int Seed,
    IReadOnlyList<PayloadBenchmarkTopology> Topologies,
    bool ChunkSweep,
    IReadOnlyList<int> ChunkSizes,
    PayloadBenchmarkTopology? WorkerTopology,
    int WorkerRepetition,
    int WorkerPosition,
    int? WorkerChunkSize,
    string ArtifactDirectory,
    string? ResultPath)
{
    public static bool TryParse(string[] args, out PayloadLatencyOptions? options)
    {
        options = null;
        var worker = args.Contains("--latency-worker", StringComparer.OrdinalIgnoreCase);
        if (!worker && !args.Contains("--latency", StringComparer.OrdinalIgnoreCase))
            return false;

        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--latency",
            "--latency-worker",
            "--chunk-sweep"
        };
        var valueOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--payload-bytes",
            "--concurrency",
            "--requests",
            "--warmup",
            "--repetitions",
            "--seed",
            "--topologies",
            "--chunk-sizes",
            "--topology",
            "--repetition",
            "--position",
            "--chunk-size",
            "--artifacts",
            "--result"
        };
        var values = ParseValues(args, flags, valueOptions);
        var topologies = ParseTopologies(values.GetValueOrDefault("--topologies"));
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var artifactDirectory = Path.GetFullPath(values.GetValueOrDefault("--artifacts")
            ?? Path.Combine("BenchmarkDotNet.Artifacts", "latency", timestamp));
        PayloadBenchmarkTopology? workerTopology = worker
            ? ParseTopology(Require(values, "--topology"))
            : null;

        options = new PayloadLatencyOptions(
            worker,
            ParsePositive(values, "--payload-bytes", 1_048_576),
            ParsePositive(values, "--concurrency", 500),
            ParsePositive(values, "--requests", 5_000),
            ParseNonNegative(values, "--warmup", 200),
            ParsePositive(values, "--repetitions", 3),
            ParseInt(values, "--seed", 73_421),
            topologies,
            args.Contains("--chunk-sweep", StringComparer.OrdinalIgnoreCase),
            ParsePositiveValues(
                values.GetValueOrDefault("--chunk-sizes"),
                BenchmarkTuning.HeaderAlignedChunkSweepBytes),
            workerTopology,
            ParsePositive(values, "--repetition", 1),
            ParsePositive(values, "--position", 1),
            values.TryGetValue("--chunk-size", out var workerChunk)
                ? ParsePositiveValue("--chunk-size", workerChunk)
                : null,
            artifactDirectory,
            worker ? Path.GetFullPath(Require(values, "--result")) : null);
        return true;
    }

    private static Dictionary<string, string> ParseValues(
        string[] args,
        IReadOnlySet<string> flags,
        IReadOnlySet<string> valueOptions)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || flags.Contains(args[index]))
                continue;
            if (!valueOptions.Contains(args[index]))
                throw new ArgumentException($"Unknown latency option '{args[index]}'.");
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Missing value for latency option '{args[index]}'.");
            values[args[index]] = args[++index];
        }
        return values;
    }

    private static IReadOnlyList<PayloadBenchmarkTopology> ParseTopologies(string? configured) =>
        string.IsNullOrWhiteSpace(configured) || configured.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? Enum.GetValues<PayloadBenchmarkTopology>()
            : configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParseTopology)
                .Distinct()
                .ToArray();

    private static PayloadBenchmarkTopology ParseTopology(string value)
    {
        var match = Enum.GetValues<PayloadBenchmarkTopology>()
            .FirstOrDefault(topology => topology.Label().Equals(value, StringComparison.OrdinalIgnoreCase));
        if (Enum.GetValues<PayloadBenchmarkTopology>().Contains(match) &&
            match.Label().Equals(value, StringComparison.OrdinalIgnoreCase))
            return match;
        return Enum.TryParse<PayloadBenchmarkTopology>(value, true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unknown payload topology '{value}'.");
    }

    private static IReadOnlyList<int> ParsePositiveValues(
        string? configured,
        IReadOnlyList<int> defaults) =>
        string.IsNullOrWhiteSpace(configured)
            ? defaults
            : configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => ParsePositiveValue("--chunk-sizes", value))
                .Distinct()
                .ToArray();

    private static int ParsePositiveValue(string name, string value) =>
        int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : throw new ArgumentException($"Latency option '{name}' must contain positive integers.");

    private static string Require(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value)
            ? value
            : throw new ArgumentException($"Worker option '{name}' is required.");

    private static int ParsePositive(
        IReadOnlyDictionary<string, string> values,
        string name,
        int defaultValue)
    {
        var value = ParseInt(values, name, defaultValue);
        return value > 0 ? value : throw new ArgumentOutOfRangeException(name, "Value must be positive.");
    }

    private static int ParseNonNegative(
        IReadOnlyDictionary<string, string> values,
        string name,
        int defaultValue)
    {
        var value = ParseInt(values, name, defaultValue);
        return value >= 0 ? value : throw new ArgumentOutOfRangeException(name, "Value cannot be negative.");
    }

    private static int ParseInt(
        IReadOnlyDictionary<string, string> values,
        string name,
        int defaultValue) =>
        values.TryGetValue(name, out var configured) && int.TryParse(configured, out var parsed)
            ? parsed
            : values.ContainsKey(name)
                ? throw new ArgumentException($"Latency option '{name}' must be an integer.")
                : defaultValue;
}

internal sealed record LatencyStatistics(
    double MeanMilliseconds,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    double MaxMilliseconds)
{
    public static LatencyStatistics Calculate(IReadOnlyList<long> timestampDurations)
    {
        if (timestampDurations.Count == 0)
            throw new ArgumentException("At least one duration is required.", nameof(timestampDurations));

        var sorted = timestampDurations.Order().ToArray();
        var millisecondsPerTimestamp = 1_000d / Stopwatch.Frequency;
        return new LatencyStatistics(
            sorted.Average() * millisecondsPerTimestamp,
            Percentile(sorted, 0.50) * millisecondsPerTimestamp,
            Percentile(sorted, 0.95) * millisecondsPerTimestamp,
            Percentile(sorted, 0.99) * millisecondsPerTimestamp,
            sorted[^1] * millisecondsPerTimestamp);
    }

    private static long Percentile(long[] sorted, double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(percentile * sorted.Length) - 1, 0, sorted.Length - 1);
        return sorted[index];
    }
}

internal sealed record BenchmarkBlock<T>(int Repetition, int Position, T Value);

internal static class BenchmarkBlockRandomizer
{
    public static IReadOnlyList<BenchmarkBlock<T>> Create<T>(
        IReadOnlyList<T> values,
        int repetitions,
        int seed)
    {
        if (values.Count == 0)
            throw new ArgumentException("At least one block value is required.", nameof(values));
        if (repetitions <= 0)
            throw new ArgumentOutOfRangeException(nameof(repetitions));

        var random = new Random(seed);
        var blocks = new List<BenchmarkBlock<T>>(values.Count * repetitions);
        for (var repetition = 1; repetition <= repetitions; repetition++)
        {
            var shuffled = values.ToArray();
            for (var index = shuffled.Length - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);
                (shuffled[index], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[index]);
            }

            for (var position = 0; position < shuffled.Length; position++)
                blocks.Add(new BenchmarkBlock<T>(repetition, position + 1, shuffled[position]));
        }
        return blocks;
    }
}

internal sealed record PayloadLatencyVariant(PayloadBenchmarkTopology Topology, int? ChunkSize)
{
    public string Label => ChunkSize.HasValue
        ? $"{Topology.Label()}_Chunk_{ChunkSize.Value}"
        : Topology.Label();
}

internal sealed record PayloadLatencyWorkerResult(
    int Repetition,
    int Position,
    int ProcessId,
    string Topology,
    int? ChunkSize,
    int PayloadBytes,
    int Concurrency,
    int Requests,
    int WarmupRequests,
    double ElapsedMilliseconds,
    double ThroughputRequestsPerSecond,
    LatencyStatistics Latency,
    double CpuMilliseconds,
    int Gen2Collections,
    long BaselineWorkingSetBytes,
    long PeakWorkingSetBytes,
    long PeakWorkingSetDeltaBytes,
    long BaselineManagedHeapBytes,
    long PeakManagedHeapBytes,
    long PeakManagedHeapDeltaBytes,
    string CoordinatedOmissionLimitation,
    JsonElement EffectiveSettings);

internal static class PayloadLatencyHarness
{
    internal const string CoordinatedOmissionLimitation =
        "Closed-loop workers issue a new request only after the previous request completes; " +
        "the results can understate queueing tails caused by coordinated omission.";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static Task<int> RunAsync(PayloadLatencyOptions options) =>
        options.Worker ? RunWorkerAsync(options) : RunOrchestratorAsync(options);

    private static async Task<int> RunOrchestratorAsync(PayloadLatencyOptions options)
    {
        Directory.CreateDirectory(options.ArtifactDirectory);
        var variants = options.ChunkSweep
            ? options.Topologies.Where(static topology => topology.IsBolt())
                .SelectMany(topology => options.ChunkSizes.Select(chunk => new PayloadLatencyVariant(topology, chunk)))
                .ToArray()
            : options.Topologies.Select(static topology => new PayloadLatencyVariant(topology, null)).ToArray();
        if (variants.Length == 0)
            throw new InvalidOperationException("Chunk sweep requires at least one Bolt topology.");

        var blocks = BenchmarkBlockRandomizer.Create(variants, options.Repetitions, options.Seed);
        var results = new List<PayloadLatencyWorkerResult>(blocks.Count);
        foreach (var block in blocks)
        {
            var blockDirectory = Path.Combine(
                options.ArtifactDirectory,
                $"r{block.Repetition}-p{block.Position}-{block.Value.Label}");
            var resultPath = Path.Combine(blockDirectory, "worker-result.json");
            var arguments = CreateWorkerArguments(options, block, blockDirectory, resultPath);
            var environment = new Dictionary<string, string?>
            {
                ["BOLT_BENCH_ARTIFACTS"] = blockDirectory
            };
            if (block.Value.ChunkSize.HasValue)
                environment["BOLT_BENCH_STREAM_CHUNK_SIZE_BYTES"] = block.Value.ChunkSize.Value.ToString();

            var child = await BenchmarkChildProcess.RunAsync(arguments, blockDirectory, environment);
            if (child.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Latency worker failed with exit code {child.ExitCode}. See {child.StandardErrorPath}.");
            if (!File.Exists(resultPath))
                throw new InvalidOperationException($"Latency worker did not write {resultPath}.");
            results.Add(JsonSerializer.Deserialize<PayloadLatencyWorkerResult>(
                await File.ReadAllTextAsync(resultPath), JsonOptions)
                ?? throw new InvalidOperationException($"Latency worker result {resultPath} was empty."));
        }

        var artifactPath = Path.Combine(options.ArtifactDirectory, "request-latency-results.json");
        var artifact = new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            mode = options.ChunkSweep ? "chunk-sweep" : "topology-comparison",
            executionIsolation = "one fresh child process per randomized block",
            memoryAccounting = "peak minus post-warmup baseline in each isolated worker",
            coordinatedOmissionLimitation = CoordinatedOmissionLimitation,
            options,
            blocks = blocks.Select(static block => new
            {
                block.Repetition,
                block.Position,
                block.Value.Topology,
                block.Value.ChunkSize,
                block.Value.Label
            }),
            results
        };
        await File.WriteAllTextAsync(artifactPath, JsonSerializer.Serialize(artifact, JsonOptions));
        Console.WriteLine("Latency artifact: {0}", Path.GetFullPath(artifactPath));
        return 0;
    }

    private static async Task<int> RunWorkerAsync(PayloadLatencyOptions options)
    {
        Directory.CreateDirectory(options.ArtifactDirectory);
        var topology = options.WorkerTopology!.Value;
        await using var environment = await PayloadBenchmarkEnvironment.CreateAsync(
            topology,
            options.PayloadBytes,
            $"PayloadLatencyWorker.{topology.Label()}.R{options.WorkerRepetition}");

        var warmupRequests = Math.Max(options.WarmupRequests, options.Concurrency);
        await RunRequestsAsync(environment, topology, warmupRequests, options.Concurrency, captureLatency: false);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false);

        var process = Process.GetCurrentProcess();
        process.Refresh();
        var baselineWorkingSet = process.WorkingSet64;
        var baselineHeap = GC.GetTotalMemory(forceFullCollection: false);
        var cpuBefore = process.TotalProcessorTime;
        var gen2Before = GC.CollectionCount(2);
        using var sampler = new ProcessMemorySampler(process, baselineWorkingSet, baselineHeap);
        var elapsed = Stopwatch.StartNew();
        var durations = await RunRequestsAsync(
            environment,
            topology,
            options.Requests,
            options.Concurrency,
            captureLatency: true);
        elapsed.Stop();
        sampler.Stop();

        var result = new PayloadLatencyWorkerResult(
            options.WorkerRepetition,
            options.WorkerPosition,
            Environment.ProcessId,
            topology.Label(),
            options.WorkerChunkSize,
            options.PayloadBytes,
            options.Concurrency,
            options.Requests,
            warmupRequests,
            elapsed.Elapsed.TotalMilliseconds,
            options.Requests / elapsed.Elapsed.TotalSeconds,
            LatencyStatistics.Calculate(durations),
            (process.TotalProcessorTime - cpuBefore).TotalMilliseconds,
            GC.CollectionCount(2) - gen2Before,
            baselineWorkingSet,
            sampler.PeakWorkingSetBytes,
            Math.Max(0, sampler.PeakWorkingSetBytes - baselineWorkingSet),
            baselineHeap,
            sampler.PeakManagedHeapBytes,
            Math.Max(0, sampler.PeakManagedHeapBytes - baselineHeap),
            CoordinatedOmissionLimitation,
            JsonSerializer.SerializeToElement(environment.GetEffectiveSettings(), JsonOptions));
        await File.WriteAllTextAsync(options.ResultPath!, JsonSerializer.Serialize(result, JsonOptions));
        Console.WriteLine(
            "{0} mean={1:F3} p95={2:F3} p99={3:F3} ms throughput={4:F0}/s",
            topology.Label(),
            result.Latency.MeanMilliseconds,
            result.Latency.P95Milliseconds,
            result.Latency.P99Milliseconds,
            result.ThroughputRequestsPerSecond);
        return 0;
    }

    private static string[] CreateWorkerArguments(
        PayloadLatencyOptions options,
        BenchmarkBlock<PayloadLatencyVariant> block,
        string artifactDirectory,
        string resultPath)
    {
        var arguments = new List<string>
        {
            "--latency-worker",
            "--payload-bytes", options.PayloadBytes.ToString(),
            "--concurrency", options.Concurrency.ToString(),
            "--requests", options.Requests.ToString(),
            "--warmup", options.WarmupRequests.ToString(),
            "--repetition", block.Repetition.ToString(),
            "--position", block.Position.ToString(),
            "--topology", block.Value.Topology.Label(),
            "--artifacts", artifactDirectory,
            "--result", resultPath
        };
        if (block.Value.ChunkSize.HasValue)
        {
            arguments.Add("--chunk-size");
            arguments.Add(block.Value.ChunkSize.Value.ToString());
        }
        return arguments.ToArray();
    }

    internal static async Task<long[]> RunRequestsAsync(
        PayloadBenchmarkEnvironment environment,
        PayloadBenchmarkTopology topology,
        int requestCount,
        int concurrency,
        bool captureLatency)
    {
        var durations = captureLatency ? new long[requestCount] : [];
        var nextRequest = -1;
        var workers = new Task[Math.Min(concurrency, requestCount)];
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        for (var workerIndex = 0; workerIndex < workers.Length; workerIndex++)
        {
            workers[workerIndex] = Task.Run(async () =>
            {
                await start.Task;
                while (true)
                {
                    var requestIndex = Interlocked.Increment(ref nextRequest);
                    if (requestIndex >= requestCount)
                        return;
                    var started = captureLatency ? Stopwatch.GetTimestamp() : 0;
                    if (topology.IsBolt())
                        await environment.InvokeBoltAsync();
                    else
                        await environment.InvokeGrpcAsync();
                    if (captureLatency)
                        durations[requestIndex] = Stopwatch.GetTimestamp() - started;
                }
            });
        }
        start.SetResult();
        await Task.WhenAll(workers);
        return durations;
    }
}

internal sealed class ProcessMemorySampler : IDisposable
{
    private readonly Process _process;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _samplingTask;
    private bool _stopped;

    public ProcessMemorySampler(Process process, long baselineWorkingSet, long baselineManagedHeap)
    {
        _process = process;
        PeakWorkingSetBytes = baselineWorkingSet;
        PeakManagedHeapBytes = baselineManagedHeap;
        _samplingTask = SampleAsync();
    }

    public long PeakWorkingSetBytes { get; private set; }

    public long PeakManagedHeapBytes { get; private set; }

    public void Stop()
    {
        if (_stopped)
            return;
        _stopped = true;
        _stop.Cancel();
        try
        {
            _samplingTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        Sample();
    }

    private async Task SampleAsync()
    {
        while (true)
        {
            await Task.Delay(20, _stop.Token);
            Sample();
        }
    }

    private void Sample()
    {
        _process.Refresh();
        PeakWorkingSetBytes = Math.Max(PeakWorkingSetBytes, _process.WorkingSet64);
        PeakManagedHeapBytes = Math.Max(PeakManagedHeapBytes, GC.GetTotalMemory(forceFullCollection: false));
    }

    public void Dispose()
    {
        Stop();
        _stop.Dispose();
    }
}
