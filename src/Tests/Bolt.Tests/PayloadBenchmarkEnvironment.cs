using System.Net;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using Bolt.Client;
using Bolt.Server;
using Bolt.Tests.Grpc;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using MemoryPack;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Bolt.Tests;

internal enum PayloadBenchmarkTopology
{
    RoutedBolt,
    DirectGrpc,
    DirectBolt,
    RoutedGrpc
}

internal static class PayloadBenchmarkTopologyExtensions
{
    public static string Label(this PayloadBenchmarkTopology topology) => topology switch
    {
        PayloadBenchmarkTopology.RoutedBolt => "Routed_Bolt",
        PayloadBenchmarkTopology.DirectGrpc => "Direct_gRPC",
        PayloadBenchmarkTopology.DirectBolt => "Direct_Bolt",
        PayloadBenchmarkTopology.RoutedGrpc => "Routed_gRPC",
        _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null)
    };

    public static bool IsBolt(this PayloadBenchmarkTopology topology) =>
        topology is PayloadBenchmarkTopology.RoutedBolt or PayloadBenchmarkTopology.DirectBolt;

    public static bool IsRouted(this PayloadBenchmarkTopology topology) =>
        topology is PayloadBenchmarkTopology.RoutedBolt or PayloadBenchmarkTopology.RoutedGrpc;

    public static string PathDescription(this PayloadBenchmarkTopology topology) => topology switch
    {
        PayloadBenchmarkTopology.RoutedBolt => "caller -> Bolt Hub -> Bolt service",
        PayloadBenchmarkTopology.DirectGrpc => "caller -> gRPC backend",
        PayloadBenchmarkTopology.DirectBolt => "caller -> Bolt server",
        PayloadBenchmarkTopology.RoutedGrpc => "caller -> gRPC router -> gRPC backend",
        _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null)
    };
}

internal sealed class PayloadBenchmarkEnvironment : IAsyncDisposable
{
    private const int MaxGrpcMessageBytes = 64 * 1024 * 1024;
    internal const int GrpcHttp2MaxStreamsPerConnection = 2_048;
    private readonly List<BenchmarkServerHost> _hosts = [];
    private readonly List<GrpcChannel> _grpcChannels = [];
    private readonly TimeSpan _callTimeout;
    private Task _serverFailure = null!;
    private BoltClient? _boltService;
    private BoltClient? _boltCaller;
    private GrpcChannel? _grpcCallerChannel;
    private HelloService.HelloServiceClient? _grpcClient;
    private byte[]? _boltPayload;
    private PayloadRequest? _grpcRequest;
    private string _boltTarget = "_";

    private PayloadBenchmarkEnvironment(
        PayloadBenchmarkTopology topology,
        int payloadBytes,
        TimeSpan callTimeout)
    {
        Topology = topology;
        PayloadBytes = payloadBytes;
        _callTimeout = callTimeout;
    }

    public PayloadBenchmarkTopology Topology { get; }

    public int PayloadBytes { get; }

    public int BoltLargePayloadThreshold { get; private set; }

    public static async Task<PayloadBenchmarkEnvironment> CreateAsync(
        PayloadBenchmarkTopology topology,
        int payloadBytes,
        string artifactSource)
    {
        var timeout = TimeSpan.FromSeconds(BenchmarkTuning.GetPositiveInt(
            "BOLT_BENCH_CALL_TIMEOUT_SECONDS",
            60));
        var environment = new PayloadBenchmarkEnvironment(topology, payloadBytes, timeout);

        try
        {
            if (topology.IsBolt())
                await environment.SetupBoltAsync(topology.IsRouted());
            else
                await environment.SetupGrpcAsync(topology.IsRouted());

            environment._serverFailure = BenchmarkServerHost.ObserveUnexpectedExitAsync(
                environment._hosts.ToArray());
            await environment.PreflightAsync();
            BenchmarkArtifactRecorder.Record(
                artifactSource,
                environment.GetEffectiveSettings());
            return environment;
        }
        catch
        {
            await environment.DisposeAsync();
            throw;
        }
    }

    public async Task<(HttpStatusCode StatusCode, ReadOnlyMemory<byte> Payload)> InvokeBoltAsync()
    {
        using var deadline = new CancellationTokenSource(_callTimeout);
        var operation = _boltCaller!.InvokeAsync(
            _boltTarget,
            "echo",
            _boltPayload!,
            deadline.Token);
        var response = await BenchmarkCallDeadline.AwaitAsync(
            operation,
            deadline,
            _serverFailure,
            Topology.Label());
        BenchmarkResponseValidation.ValidateBoltPayload(response, _boltPayload!);
        return response;
    }

    public async Task<PayloadReply> InvokeGrpcAsync()
    {
        using var deadline = new CancellationTokenSource(_callTimeout);
        using var call = _grpcClient!.EchoPayloadAsync(
            _grpcRequest!,
            cancellationToken: deadline.Token);
        var response = await BenchmarkCallDeadline.AwaitAsync(
            call.ResponseAsync,
            deadline,
            _serverFailure,
            Topology.Label());
        BenchmarkResponseValidation.ValidateGrpcPayload(response, _grpcRequest!.Data);
        return response;
    }

    public IReadOnlyDictionary<string, object?> GetEffectiveSettings() =>
        new Dictionary<string, object?>
        {
            ["topology"] = Topology.Label(),
            ["logicalPath"] = Topology.PathDescription(),
            ["payloadBytes"] = PayloadBytes,
            ["callTimeoutSeconds"] = _callTimeout.TotalSeconds,
            ["largePayloadThresholdBytes"] = BoltLargePayloadThreshold,
            ["streamChunkSizeBytes"] = Topology.IsBolt()
                ? BenchmarkTuning.GetPositiveInt(
                    "BOLT_BENCH_STREAM_CHUNK_SIZE_BYTES",
                    new BoltClientOptions().StreamChunkSize)
                : null,
            ["pipelineBytes"] = Topology.IsBolt()
                ? BenchmarkTuning.GetPositiveInt(
                    "BOLT_BENCH_PIPELINE_BYTES",
                    new BoltClientOptions().MaxLargeRpcPipelineBytes)
                : null,
            ["receiveBufferBytes"] = Topology.IsBolt()
                ? BenchmarkTuning.GetPositiveInt(
                    "BOLT_BENCH_RECEIVE_BUFFER_BYTES",
                    new BoltClientOptions().ReceiveBufferBytes)
                : null,
            ["hubReceiveBufferBytes"] = Topology.IsBolt()
                ? BenchmarkTuning.GetPositiveInt(
                    "BOLT_BENCH_HUB_RECEIVE_BUFFER_BYTES",
                    256 * 1024)
                : null,
            ["runRpcContinuationsAsynchronously"] = Topology.IsBolt()
                ? BenchmarkTuning.GetBoolean(
                    "BOLT_BENCH_RUN_RPC_CONTINUATIONS_ASYNCHRONOUSLY",
                    new BoltClientOptions().RunRpcContinuationsAsynchronously)
                : null,
            ["grpcHttp2MaxStreamsPerConnection"] = Topology.IsBolt()
                ? null
                : GrpcHttp2MaxStreamsPerConnection,
            ["grpcCallerChannelCount"] = Topology.IsBolt() ? null : 1,
            ["grpcBackendChannelCount"] = Topology == PayloadBenchmarkTopology.RoutedGrpc ? 1 : 0,
            ["grpcMultipleHttp2ConnectionsEnabled"] = Topology.IsBolt() ? null : false,
            ["serverAddresses"] = _hosts.Select(static host => host.BaseAddress.ToString()).ToArray()
        };

    private async Task SetupBoltAsync(bool routed)
    {
        var builder = WebApplication.CreateBuilder();
        BenchmarkServerHost.ConfigureDynamicLoopback(builder, HttpProtocols.Http1);
        builder.Services.AddBoltServer(options =>
        {
            options.MaxPendingRpcCalls = 2_048;
            options.MaxPendingRpcCallsPerPrincipal = 2_048;
            options.ReceiveBufferBytes = BenchmarkTuning.GetPositiveInt(
                "BOLT_BENCH_HUB_RECEIVE_BUFFER_BYTES",
                options.ReceiveBufferBytes);
        });
        builder.Logging.ClearProviders();

        var app = builder.Build();
        if (!routed)
            app.Services.GetRequiredService<BoltServer>().RegisterHandler("echo", EchoBoltPayload);
        app.UseWebSockets();
        app.MapBolt("/bolt");

        var host = await BenchmarkServerHost.StartAsync(
            routed ? "Bolt Hub" : "Bolt direct server",
            app);
        _hosts.Add(host);

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        var options = CreateBoltOptions();
        BoltLargePayloadThreshold = options.LargePayloadThreshold;
        var webSocketAddress = new UriBuilder(host.BaseAddress)
        {
            Scheme = "ws",
            Path = "/bolt"
        }.Uri;

        if (routed)
        {
            _boltTarget = "payload_svc";
            _boltService = new BoltClient(
                webSocketAddress,
                "payload_svc",
                "PayloadSvc",
                options,
                loggerFactory.CreateLogger<BoltClient>());
            _boltService.RegisterHandler("echo", EchoBoltPayload);
            await _boltService.ConnectAsync();
        }

        _boltCaller = new BoltClient(
            webSocketAddress,
            routed ? "payload_caller" : "payload_direct_caller",
            routed ? "PayloadCaller" : "PayloadDirectCaller",
            options,
            loggerFactory.CreateLogger<BoltClient>());
        await _boltCaller.ConnectAsync();

        _boltPayload = MemoryPackSerializer.Serialize(new BenchPayload { Data = GeneratePayloadData() });
    }

    private async Task SetupGrpcAsync(bool routed)
    {
        if (routed)
        {
            var backendBuilder = CreateGrpcBuilder();
            var backendApp = backendBuilder.Build();
            backendApp.MapGrpcService<GrpcEchoPayloadBackend>();
            var backendHost = await BenchmarkServerHost.StartAsync("gRPC backend", backendApp);
            _hosts.Add(backendHost);

            var backendChannel = CreateGrpcChannel(backendHost.BaseAddress);
            _grpcChannels.Add(backendChannel);
            var backendClient = new HelloService.HelloServiceClient(backendChannel);

            var routerBuilder = CreateGrpcBuilder();
            routerBuilder.Services.AddSingleton(backendClient);
            var routerApp = routerBuilder.Build();
            routerApp.MapGrpcService<GrpcEchoPayloadRouter>();
            var routerHost = await BenchmarkServerHost.StartAsync("gRPC router", routerApp);
            _hosts.Add(routerHost);
            _grpcCallerChannel = CreateGrpcChannel(routerHost.BaseAddress);
        }
        else
        {
            var builder = CreateGrpcBuilder();
            var app = builder.Build();
            app.MapGrpcService<GrpcEchoPayloadBackend>();
            var host = await BenchmarkServerHost.StartAsync("gRPC direct server", app);
            _hosts.Add(host);
            _grpcCallerChannel = CreateGrpcChannel(host.BaseAddress);
        }

        _grpcChannels.Add(_grpcCallerChannel);
        _grpcClient = new HelloService.HelloServiceClient(_grpcCallerChannel);
        _grpcRequest = new PayloadRequest { Data = ByteString.CopyFrom(GeneratePayloadData()) };
    }

    private WebApplicationBuilder CreateGrpcBuilder()
    {
        var builder = WebApplication.CreateBuilder();
        BenchmarkServerHost.ConfigureDynamicLoopback(builder, HttpProtocols.Http2);
        builder.WebHost.ConfigureKestrel(options =>
            options.Limits.Http2.MaxStreamsPerConnection = GrpcHttp2MaxStreamsPerConnection);
        builder.Services.AddGrpc(options =>
        {
            options.MaxReceiveMessageSize = MaxGrpcMessageBytes;
            options.MaxSendMessageSize = MaxGrpcMessageBytes;
        });
        builder.Logging.ClearProviders();
        return builder;
    }

    private static GrpcChannel CreateGrpcChannel(Uri address) =>
        GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            MaxReceiveMessageSize = MaxGrpcMessageBytes,
            MaxSendMessageSize = MaxGrpcMessageBytes,
            HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = false }
        });

    private BoltClientOptions CreateBoltOptions() => new()
    {
        RpcTimeoutSeconds = Math.Max(1, (int)Math.Ceiling(_callTimeout.TotalSeconds)),
        MinConnections = 1,
        MaxConnections = 1,
        MaxConcurrentInboundHandlers = 2_048,
        LargePayloadThreshold = BenchmarkTuning.GetPositiveInt(
            "BOLT_BENCH_LARGE_PAYLOAD_THRESHOLD_BYTES",
            new BoltClientOptions().LargePayloadThreshold),
        StreamChunkSize = BenchmarkTuning.GetPositiveInt(
            "BOLT_BENCH_STREAM_CHUNK_SIZE_BYTES",
            new BoltClientOptions().StreamChunkSize),
        MaxLargeRpcPipelineBytes = BenchmarkTuning.GetPositiveInt(
            "BOLT_BENCH_PIPELINE_BYTES",
            new BoltClientOptions().MaxLargeRpcPipelineBytes),
        ReceiveBufferBytes = BenchmarkTuning.GetPositiveInt(
            "BOLT_BENCH_RECEIVE_BUFFER_BYTES",
            new BoltClientOptions().ReceiveBufferBytes),
        RunRpcContinuationsAsynchronously = BenchmarkTuning.GetBoolean(
            "BOLT_BENCH_RUN_RPC_CONTINUATIONS_ASYNCHRONOUSLY",
            new BoltClientOptions().RunRpcContinuationsAsynchronously)
    };

    private async Task PreflightAsync()
    {
        if (Topology.IsBolt())
            await InvokeBoltAsync();
        else
            await InvokeGrpcAsync();
    }

    private byte[] GeneratePayloadData()
    {
        var data = new byte[PayloadBytes];
        Random.Shared.NextBytes(data);
        return data;
    }

    private static Task<(HttpStatusCode, ReadOnlyMemory<byte>)> EchoBoltPayload(
        ReadOnlyMemory<byte> payload,
        Guid _) =>
        Task.FromResult((HttpStatusCode.OK, payload));

    public async ValueTask DisposeAsync()
    {
        if (_boltCaller is not null)
            await _boltCaller.DisposeAsync();
        if (_boltService is not null)
            await _boltService.DisposeAsync();

        foreach (var channel in _grpcChannels)
            channel.Dispose();

        for (var index = _hosts.Count - 1; index >= 0; index--)
            await _hosts[index].DisposeAsync();
    }
}

public sealed class GrpcEchoPayloadRouter : HelloService.HelloServiceBase
{
    private readonly HelloService.HelloServiceClient _backend;

    public GrpcEchoPayloadRouter(HelloService.HelloServiceClient backend) => _backend = backend;

    public override async Task<PayloadReply> EchoPayload(
        PayloadRequest request,
        ServerCallContext context) =>
        await _backend.EchoPayloadAsync(
            request,
            cancellationToken: context.CancellationToken);
}

internal static class BenchmarkTuning
{
    public static IReadOnlyList<int> HeaderAlignedChunkSweepBytes { get; } =
        [65_515, 131_051, 262_123, 262_144];

    public static int GetPositiveInt(string variableName, int defaultValue) =>
        int.TryParse(Environment.GetEnvironmentVariable(variableName), out var value) && value > 0
            ? value
            : defaultValue;

    public static bool GetBoolean(string variableName, bool defaultValue)
    {
        var configured = Environment.GetEnvironmentVariable(variableName);
        return configured?.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "yes" => true,
            "false" or "0" or "no" => false,
            _ => defaultValue
        };
    }

    public static IEnumerable<int> GetPositiveValues(string variableName, int[] defaults)
    {
        var configured = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(configured))
            return defaults;

        var values = configured
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static value => int.TryParse(value, out var parsed) ? parsed : 0)
            .Where(static value => value > 0)
            .Distinct()
            .ToArray();
        return values.Length > 0 ? values : defaults;
    }
}

internal static class BenchmarkArtifactRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string Record(
        string source,
        IReadOnlyDictionary<string, object?> effectiveSettings,
        string? artifactDirectory = null)
    {
        var directory = artifactDirectory
            ?? Environment.GetEnvironmentVariable("BOLT_BENCH_ARTIFACTS")
            ?? Path.Combine("BenchmarkDotNet.Artifacts", "integrity");
        Directory.CreateDirectory(directory);

        var environment = Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Where(static entry =>
                entry.Key is string key &&
                (key.StartsWith("BOLT_BENCH_", StringComparison.Ordinal) ||
                 key.StartsWith("DOTNET_", StringComparison.Ordinal) ||
                 key.StartsWith("COMPlus_", StringComparison.Ordinal)))
            .ToDictionary(
                static entry => (string)entry.Key,
                static entry => entry.Value?.ToString(),
                StringComparer.Ordinal);

        var snapshot = new
        {
            source,
            timestampUtc = DateTimeOffset.UtcNow,
            processId = Environment.ProcessId,
            runtime = RuntimeInformation.FrameworkDescription,
            runtimeVersion = Environment.Version.ToString(),
            operatingSystem = RuntimeInformation.OSDescription,
            processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            processorCount = Environment.ProcessorCount,
            serverGc = GCSettings.IsServerGC,
            latencyMode = GCSettings.LatencyMode.ToString(),
            processPriority = GetProcessPriority(),
            effectiveSettings,
            environment
        };

        var safeSource = string.Concat(source.Select(static character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));
        var path = Path.Combine(
            directory,
            $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Environment.ProcessId}-{safeSource}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, JsonOptions));
        return Path.GetFullPath(path);
    }

    private static string GetProcessPriority()
    {
        try
        {
            return System.Diagnostics.Process.GetCurrentProcess().PriorityClass.ToString();
        }
        catch
        {
            return "unavailable";
        }
    }
}
