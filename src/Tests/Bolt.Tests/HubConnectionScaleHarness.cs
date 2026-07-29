using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bolt.Client;
using Bolt.Server;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Bolt.Tests;

internal enum HubConnectionWorkload
{
    Idle,
    Active
}

internal sealed record HubConnectionScaleOptions(
    bool Worker,
    IReadOnlyList<int> ClientCounts,
    IReadOnlyList<int> HubReceiveBufferBytes,
    IReadOnlyList<HubConnectionWorkload> Workloads,
    int PayloadBytes,
    int ActiveRequests,
    int ActiveConcurrency,
    int IdleSeconds,
    int Repetitions,
    int Seed,
    int WorkerClients,
    int WorkerReceiveBufferBytes,
    HubConnectionWorkload WorkerWorkload,
    int WorkerRepetition,
    int WorkerPosition,
    string ArtifactDirectory,
    string? ResultPath)
{
    public static bool TryParse(string[] args, out HubConnectionScaleOptions? options)
    {
        options = null;
        var worker = args.Contains("--hub-connection-scale-worker", StringComparer.OrdinalIgnoreCase);
        if (!worker && !args.Contains("--hub-connection-scale", StringComparer.OrdinalIgnoreCase))
            return false;

        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--hub-connection-scale",
            "--hub-connection-scale-worker"
        };
        var valueOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--clients",
            "--hub-receive-buffers",
            "--workloads",
            "--payload-bytes",
            "--active-requests",
            "--active-concurrency",
            "--idle-seconds",
            "--repetitions",
            "--seed",
            "--worker-clients",
            "--worker-receive-buffer",
            "--worker-workload",
            "--repetition",
            "--position",
            "--artifacts",
            "--result"
        };
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || flags.Contains(args[index]))
                continue;
            if (!valueOptions.Contains(args[index]))
                throw new ArgumentException($"Unknown Hub scale option '{args[index]}'.");
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Missing value for Hub scale option '{args[index]}'.");
            values[args[index]] = args[++index];
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        options = new HubConnectionScaleOptions(
            worker,
            ParseValues(values.GetValueOrDefault("--clients"), [100, 500, 1_000]),
            ParseValues(values.GetValueOrDefault("--hub-receive-buffers"), [64 * 1024, 128 * 1024, 256 * 1024]),
            ParseWorkloads(values.GetValueOrDefault("--workloads")),
            ParsePositive(values, "--payload-bytes", 1_024),
            ParsePositive(values, "--active-requests", 2_000),
            ParsePositive(values, "--active-concurrency", 100),
            ParsePositive(values, "--idle-seconds", 2),
            ParsePositive(values, "--repetitions", 1),
            ParseInt(values, "--seed", 91_337),
            ParsePositive(values, "--worker-clients", 1),
            ParsePositive(values, "--worker-receive-buffer", 64 * 1024),
            values.TryGetValue("--worker-workload", out var workload)
                ? Enum.Parse<HubConnectionWorkload>(workload, ignoreCase: true)
                : HubConnectionWorkload.Idle,
            ParsePositive(values, "--repetition", 1),
            ParsePositive(values, "--position", 1),
            Path.GetFullPath(values.GetValueOrDefault("--artifacts")
                ?? Path.Combine("BenchmarkDotNet.Artifacts", "hub-connection-scale", timestamp)),
            worker ? Path.GetFullPath(Require(values, "--result")) : null);
        return true;
    }

    private static IReadOnlyList<int> ParseValues(string? configured, int[] defaults) =>
        string.IsNullOrWhiteSpace(configured)
            ? defaults
            : configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var parsed) && parsed > 0
                    ? parsed
                    : throw new ArgumentException("Hub scale list values must be positive integers."))
                .Distinct()
                .ToArray();

    private static IReadOnlyList<HubConnectionWorkload> ParseWorkloads(string? configured) =>
        string.IsNullOrWhiteSpace(configured) || configured.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? Enum.GetValues<HubConnectionWorkload>()
            : configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.Parse<HubConnectionWorkload>(value, ignoreCase: true))
                .Distinct()
                .ToArray();

    private static int ParsePositive(
        IReadOnlyDictionary<string, string> values,
        string name,
        int defaultValue)
    {
        var value = ParseInt(values, name, defaultValue);
        return value > 0 ? value : throw new ArgumentOutOfRangeException(name);
    }

    private static int ParseInt(
        IReadOnlyDictionary<string, string> values,
        string name,
        int defaultValue) =>
        values.TryGetValue(name, out var configured) && int.TryParse(configured, out var parsed)
            ? parsed
            : values.ContainsKey(name)
                ? throw new ArgumentException($"Hub scale option '{name}' must be an integer.")
                : defaultValue;

    private static string Require(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value)
            ? value
            : throw new ArgumentException($"Worker option '{name}' is required.");
}

internal sealed record HubConnectionScaleVariant(
    int Clients,
    int HubReceiveBufferBytes,
    HubConnectionWorkload Workload)
{
    public string Label => $"{Workload}_{Clients}c_{HubReceiveBufferBytes / 1024}KiB";
}

internal sealed record HubConnectionScaleResult(
    int Repetition,
    int Position,
    int ProcessId,
    int Clients,
    int HubReceiveBufferBytes,
    HubConnectionWorkload Workload,
    int PayloadBytes,
    int Requests,
    int Concurrency,
    int WarmupRequests,
    double ElapsedMilliseconds,
    double ThroughputRequestsPerSecond,
    LatencyStatistics? Latency,
    double CpuMilliseconds,
    int Gen2Collections,
    long BaselineWorkingSetBytes,
    long PeakWorkingSetBytes,
    long PeakWorkingSetDeltaBytes,
    long BaselineManagedHeapBytes,
    long PeakManagedHeapBytes,
    long PeakManagedHeapDeltaBytes,
    string? CoordinatedOmissionLimitation,
    JsonElement EffectiveSettings);

internal static class HubConnectionScaleHarness
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static Task<int> RunAsync(HubConnectionScaleOptions options) =>
        options.Worker ? RunWorkerAsync(options) : RunOrchestratorAsync(options);

    private static async Task<int> RunOrchestratorAsync(HubConnectionScaleOptions options)
    {
        Directory.CreateDirectory(options.ArtifactDirectory);
        var variants = options.ClientCounts
            .SelectMany(clients => options.HubReceiveBufferBytes
                .SelectMany(buffer => options.Workloads
                    .Select(workload => new HubConnectionScaleVariant(clients, buffer, workload))))
            .ToArray();
        var blocks = BenchmarkBlockRandomizer.Create(variants, options.Repetitions, options.Seed);
        var results = new List<HubConnectionScaleResult>(blocks.Count);

        foreach (var block in blocks)
        {
            var directory = Path.Combine(
                options.ArtifactDirectory,
                $"r{block.Repetition}-p{block.Position}-{block.Value.Label}");
            var resultPath = Path.Combine(directory, "worker-result.json");
            var arguments = new[]
            {
                "--hub-connection-scale-worker",
                "--worker-clients", block.Value.Clients.ToString(),
                "--worker-receive-buffer", block.Value.HubReceiveBufferBytes.ToString(),
                "--worker-workload", block.Value.Workload.ToString(),
                "--payload-bytes", options.PayloadBytes.ToString(),
                "--active-requests", options.ActiveRequests.ToString(),
                "--active-concurrency", options.ActiveConcurrency.ToString(),
                "--idle-seconds", options.IdleSeconds.ToString(),
                "--repetition", block.Repetition.ToString(),
                "--position", block.Position.ToString(),
                "--artifacts", directory,
                "--result", resultPath
            };
            var child = await BenchmarkChildProcess.RunAsync(
                arguments,
                directory,
                new Dictionary<string, string?> { ["BOLT_BENCH_ARTIFACTS"] = directory },
                TimeSpan.FromMinutes(30));
            if (child.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Hub scale worker failed with exit code {child.ExitCode}. See {child.StandardErrorPath}.");
            if (!File.Exists(resultPath))
                throw new InvalidOperationException($"Hub scale worker did not write {resultPath}.");
            results.Add(JsonSerializer.Deserialize<HubConnectionScaleResult>(
                await File.ReadAllTextAsync(resultPath), JsonOptions)
                ?? throw new InvalidOperationException($"Hub scale result {resultPath} was empty."));
        }

        var artifactPath = Path.Combine(options.ArtifactDirectory, "hub-connection-scale-results.json");
        await File.WriteAllTextAsync(artifactPath, JsonSerializer.Serialize(new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            executionIsolation = "one fresh child process per randomized variant",
            memoryAccounting = "peak minus post-warmup baseline in each isolated worker",
            options,
            blocks = blocks.Select(static block => new
            {
                block.Repetition,
                block.Position,
                block.Value.Clients,
                block.Value.HubReceiveBufferBytes,
                block.Value.Workload
            }),
            results
        }, JsonOptions));
        Console.WriteLine("Hub connection-scale artifact: {0}", Path.GetFullPath(artifactPath));
        return 0;
    }

    private static async Task<int> RunWorkerAsync(HubConnectionScaleOptions options)
    {
        Directory.CreateDirectory(options.ArtifactDirectory);
        await using var environment = await HubConnectionScaleEnvironment.CreateAsync(
            options.WorkerClients,
            options.WorkerReceiveBufferBytes,
            options.PayloadBytes);

        var activeConcurrency = Math.Min(options.ActiveConcurrency, options.WorkerClients);
        var warmupRequests = Math.Max(options.WorkerClients, activeConcurrency);
        await environment.RunRequestsAsync(warmupRequests, activeConcurrency, captureLatency: false);
        await Task.Delay(250);
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
        long[] durations;
        var measuredRequests = 0;
        if (options.WorkerWorkload == HubConnectionWorkload.Active)
        {
            measuredRequests = options.ActiveRequests;
            durations = await environment.RunRequestsAsync(
                measuredRequests,
                activeConcurrency,
                captureLatency: true);
        }
        else
        {
            await Task.Delay(TimeSpan.FromSeconds(options.IdleSeconds));
            durations = [];
        }
        elapsed.Stop();
        sampler.Stop();

        var result = new HubConnectionScaleResult(
            options.WorkerRepetition,
            options.WorkerPosition,
            Environment.ProcessId,
            options.WorkerClients,
            options.WorkerReceiveBufferBytes,
            options.WorkerWorkload,
            options.PayloadBytes,
            measuredRequests,
            activeConcurrency,
            warmupRequests,
            elapsed.Elapsed.TotalMilliseconds,
            measuredRequests == 0 ? 0 : measuredRequests / elapsed.Elapsed.TotalSeconds,
            durations.Length == 0 ? null : LatencyStatistics.Calculate(durations),
            (process.TotalProcessorTime - cpuBefore).TotalMilliseconds,
            GC.CollectionCount(2) - gen2Before,
            baselineWorkingSet,
            sampler.PeakWorkingSetBytes,
            Math.Max(0, sampler.PeakWorkingSetBytes - baselineWorkingSet),
            baselineHeap,
            sampler.PeakManagedHeapBytes,
            Math.Max(0, sampler.PeakManagedHeapBytes - baselineHeap),
            options.WorkerWorkload == HubConnectionWorkload.Active
                ? PayloadLatencyHarness.CoordinatedOmissionLimitation
                : null,
            JsonSerializer.SerializeToElement(environment.GetEffectiveSettings(), JsonOptions));
        await File.WriteAllTextAsync(options.ResultPath!, JsonSerializer.Serialize(result, JsonOptions));
        Console.WriteLine(
            "{0} clients={1} buffer={2}KiB wsDelta={3:N0} heapDelta={4:N0}",
            options.WorkerWorkload,
            options.WorkerClients,
            options.WorkerReceiveBufferBytes / 1024,
            result.PeakWorkingSetDeltaBytes,
            result.PeakManagedHeapDeltaBytes);
        return 0;
    }
}

internal sealed class HubConnectionScaleEnvironment : IAsyncDisposable
{
    private readonly List<BoltClient> _callers = [];
    private readonly BenchmarkServerHost _host;
    private readonly BoltClient _service;
    private readonly byte[] _payload;
    private readonly Task _serverFailure;

    private HubConnectionScaleEnvironment(
        BenchmarkServerHost host,
        BoltClient service,
        byte[] payload,
        Task serverFailure,
        int receiveBufferBytes)
    {
        _host = host;
        _service = service;
        _payload = payload;
        _serverFailure = serverFailure;
        HubReceiveBufferBytes = receiveBufferBytes;
    }

    public int HubReceiveBufferBytes { get; }

    public static async Task<HubConnectionScaleEnvironment> CreateAsync(
        int clientCount,
        int receiveBufferBytes,
        int payloadBytes)
    {
        var builder = WebApplication.CreateBuilder();
        BenchmarkServerHost.ConfigureDynamicLoopback(builder, HttpProtocols.Http1);
        builder.Services.AddBoltServer(options =>
        {
            options.ReceiveBufferBytes = receiveBufferBytes;
            options.MaxPendingRpcCalls = 4_096;
            options.MaxPendingRpcCallsPerPrincipal = 4_096;
        });
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.UseWebSockets();
        app.MapBolt("/bolt");
        var host = await BenchmarkServerHost.StartAsync("Bolt connection-scale Hub", app);
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        var address = new UriBuilder(host.BaseAddress) { Scheme = "ws", Path = "/bolt" }.Uri;
        var options = new BoltClientOptions
        {
            MinConnections = 1,
            MaxConnections = 1,
            RpcTimeoutSeconds = 60,
            MaxConcurrentInboundHandlers = 4_096,
            RunRpcContinuationsAsynchronously = BenchmarkTuning.GetBoolean(
                "BOLT_BENCH_RUN_RPC_CONTINUATIONS_ASYNCHRONOUSLY",
                new BoltClientOptions().RunRpcContinuationsAsynchronously)
        };
        BoltClient? service = null;
        HubConnectionScaleEnvironment? environment = null;
        try
        {
            service = new BoltClient(
                address,
                "scale_service",
                "ScaleService",
                options,
                loggerFactory.CreateLogger<BoltClient>());
            service.RegisterHandler("echo", static (payload, _) =>
                Task.FromResult((HttpStatusCode.OK, payload)));
            await service.ConnectAsync();

            var payload = new byte[payloadBytes];
            Random.Shared.NextBytes(payload);
            environment = new HubConnectionScaleEnvironment(
                host,
                service,
                payload,
                BenchmarkServerHost.ObserveUnexpectedExitAsync(host),
                receiveBufferBytes);
            for (var offset = 0; offset < clientCount; offset += 64)
            {
                var batch = Enumerable.Range(offset, Math.Min(64, clientCount - offset))
                    .Select(async index =>
                    {
                        var client = new BoltClient(
                            address,
                            $"scale_caller_{index}",
                            $"ScaleCaller{index}",
                            options,
                            loggerFactory.CreateLogger<BoltClient>());
                        await client.ConnectAsync();
                        return client;
                    });
                environment._callers.AddRange(await Task.WhenAll(batch));
            }
            return environment;
        }
        catch
        {
            if (environment is not null)
                await environment.DisposeAsync();
            else
            {
                if (service is not null)
                    await service.DisposeAsync();
                await host.DisposeAsync();
            }
            throw;
        }
    }

    public IReadOnlyDictionary<string, object?> GetEffectiveSettings() =>
        new Dictionary<string, object?>
        {
            ["connectedClients"] = _callers.Count,
            ["hubReceiveBufferBytes"] = HubReceiveBufferBytes,
            ["callerConnectionsPerClient"] = 1,
            ["runRpcContinuationsAsynchronously"] = BenchmarkTuning.GetBoolean(
                "BOLT_BENCH_RUN_RPC_CONTINUATIONS_ASYNCHRONOUSLY",
                new BoltClientOptions().RunRpcContinuationsAsynchronously)
        };

    public async Task<long[]> RunRequestsAsync(int requestCount, int concurrency, bool captureLatency)
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
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    var response = await BenchmarkCallDeadline.AwaitAsync(
                        _callers[requestIndex % _callers.Count].InvokeAsync(
                            "scale_service",
                            "echo",
                            _payload,
                            deadline.Token),
                        deadline,
                        _serverFailure,
                        "Hub connection-scale RPC");
                    BenchmarkResponseValidation.ValidateBoltPayload(response, _payload);
                    if (captureLatency)
                        durations[requestIndex] = Stopwatch.GetTimestamp() - started;
                }
            });
        }
        start.SetResult();
        await Task.WhenAll(workers);
        return durations;
    }

    public async ValueTask DisposeAsync()
    {
        for (var offset = 0; offset < _callers.Count; offset += 64)
        {
            var count = Math.Min(64, _callers.Count - offset);
            await Task.WhenAll(_callers.GetRange(offset, count)
                .Select(static client => client.DisposeAsync().AsTask()));
        }
        await _service.DisposeAsync();
        await _host.DisposeAsync();
    }
}
