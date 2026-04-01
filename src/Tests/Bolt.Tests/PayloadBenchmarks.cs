using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using Bolt.Client;
using Bolt.Client.Transport;
using Bolt.Protocol.Transport;
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
/// Head-to-head payload size benchmark: Bolt (WebSocket) vs Bolt (QUIC) vs gRPC.
/// All three transports echo raw bytes of varying sizes.
///
/// Tests answer: "How do Bolt WebSocket, Bolt QUIC, and gRPC compare as payloads grow?"
///
/// Payload sizes:
/// - 100B:   tiny (metadata, status codes)
/// - 1KB:    typical chat message
/// - 32KB:   small file, JSON response
/// - 128KB:  medium (below Bolt auto-stream threshold)
/// - 512KB:  large (above 256KB → Bolt auto-streams via BoltStream)
///
/// Note: Bolt_Quic_Echo only runs meaningfully when the platform supports QUIC
/// (QuicConnection.IsSupported). On unsupported platforms it returns a dummy value.
/// </summary>
[Config(typeof(PayloadBenchConfig))]
[MemoryDiagnoser]
public class PayloadBenchmarks
{
    // Bolt (WebSocket)
    private WebApplication _boltApp = null!;
    private BoltClient _boltService = null!;
    private BoltClient _boltCaller = null!;
    private byte[] _boltPayload = null!;

    // Bolt (QUIC)
    private BoltClient? _boltQuicService;
    private BoltClient? _boltQuicCaller;
    private X509Certificate2? _quicCert;
    private CancellationTokenSource? _quicListenerCts;

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

        // Warmup all transports
        for (int i = 0; i < 10; i++)
        {
            await _boltCaller.InvokeAsync("payload_svc", "echo", _boltPayload);
            await _grpcClient.EchoPayloadAsync(_grpcRequest);
        }

        if (_boltQuicCaller != null)
        {
            for (int i = 0; i < 10; i++)
                await _boltQuicCaller.InvokeAsync("quic_payload_svc", "echo", _boltPayload);
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

        // QUIC transport (only when platform supports it)
        if (QuicConnection.IsSupported && QuicListener.IsSupported)
        {
            try
            {
                var server = _boltApp.Services.GetRequiredService<BoltServer>();
                _quicCert = GenerateSelfSignedCert();
                _quicListenerCts = new CancellationTokenSource();
                await StartQuicListenerAsync(server, 18603, _quicCert, _quicListenerCts.Token);

                var quicOpts = new BoltClientOptions
                {
                    RpcTimeoutSeconds = 60,
                    TransportAttemptTimeoutMs = 10_000,
                    PreferredTransports = [BoltTransport.Quic]
                };

                _boltQuicService = new BoltClient(new Uri("quic://127.0.0.1:18603/bolt"),
                    "quic_payload_svc", "QuicPayloadSvc", quicOpts, lf.CreateLogger<BoltClient>());
                _boltQuicService.RegisterHandler("echo", (payload, _) =>
                    Task.FromResult((HttpStatusCode.OK, payload)));
                await _boltQuicService.ConnectAsync();

                _boltQuicCaller = new BoltClient(new Uri("quic://127.0.0.1:18603/bolt"),
                    "quic_payload_caller", "QuicPayloadCaller", quicOpts, lf.CreateLogger<BoltClient>());
                await _boltQuicCaller.ConnectAsync();
            }
            catch (Exception ex)
            {
                // QUIC may report supported but fail at runtime; benchmarks degrade gracefully
                Console.WriteLine($"QUIC setup failed: {ex.Message}");
                _boltQuicService = null;
                _boltQuicCaller = null;
            }
        }
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

    [Benchmark]
    public async Task<(HttpStatusCode, ReadOnlyMemory<byte>)> Bolt_Quic_Echo()
    {
        if (_boltQuicCaller == null)
            return (HttpStatusCode.ServiceUnavailable, ReadOnlyMemory<byte>.Empty);

        return await _boltQuicCaller.InvokeAsync("quic_payload_svc", "echo", _boltPayload);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        try { if (_boltQuicCaller != null) await _boltQuicCaller.DisposeAsync(); } catch { }
        try { if (_boltQuicService != null) await _boltQuicService.DisposeAsync(); } catch { }
        _quicListenerCts?.Cancel();
        _quicCert?.Dispose();
        try { await _boltCaller.DisposeAsync(); } catch { }
        try { await _boltService.DisposeAsync(); } catch { }
        _grpcChannel?.Dispose();
        try { await _grpcApp.StopAsync(); } catch { }
        try { await _boltApp.StopAsync(); } catch { }
    }

    private static async Task StartQuicListenerAsync(BoltServer server, int port, X509Certificate2 cert, CancellationToken ct)
    {
        if (!QuicListener.IsSupported) return;

        var listener = await QuicListener.ListenAsync(new QuicListenerOptions
        {
            ListenEndPoint = new IPEndPoint(IPAddress.Loopback, port),
            ApplicationProtocols = [new SslApplicationProtocol("bolt")],
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions
            {
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 0,
                MaxInboundBidirectionalStreams = 256,
                    MaxInboundUnidirectionalStreams = 256,
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ServerCertificate = cert,
                    ApplicationProtocols = [new SslApplicationProtocol("bolt")]
                }
            })
        }, ct);

        _ = Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var quicConn = await listener.AcceptConnectionAsync(ct);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var transport = new QuicBoltConnection(quicConn);
                            transport.StartAcceptLoop(ct);
                            await server.HandleConnectionAsync(transport, ct);
                        }
                        catch { }
                    }, ct);
                }
            }
            catch (OperationCanceledException) { }
            finally { await listener.DisposeAsync(); }
        }, ct);
    }

    private static X509Certificate2 GenerateSelfSignedCert()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));
        return new X509Certificate2(cert.Export(X509ContentType.Pfx));
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

/// <summary>
/// Sustained throughput benchmark: Bolt (WebSocket) vs Bolt (QUIC) vs gRPC.
/// Fires tight sequential RPC loops to measure maximum ops/sec for each transport.
///
/// Payload sizes:
/// - 1KB:  typical RPC message (chat, API call)
/// - 64KB: medium payload (JSON document, small file)
///
/// Each benchmark invocation fires 100 sequential RPCs so BenchmarkDotNet can
/// report per-operation statistics via OperationsPerInvoke.
/// </summary>
[Config(typeof(ThroughputBenchConfig))]
[MemoryDiagnoser]
public class ThroughputBenchmarks
{
    private const int OpsPerInvocation = 100;

    // Bolt (WebSocket)
    private WebApplication _boltApp = null!;
    private BoltClient _boltService = null!;
    private BoltClient _boltCaller = null!;
    private byte[] _boltPayload = null!;

    // Bolt (QUIC)
    private BoltClient? _boltQuicService;
    private BoltClient? _boltQuicCaller;
    private X509Certificate2? _quicCert;
    private CancellationTokenSource? _quicListenerCts;

    // gRPC
    private WebApplication _grpcApp = null!;
    private GrpcChannel _grpcChannel = null!;
    private HelloService.HelloServiceClient _grpcClient = null!;
    private PayloadRequest _grpcRequest = null!;

    [Params(1024, 65_536)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        await SetupBolt();
        await SetupGrpc();
        GeneratePayloads();

        // Warmup all transports
        for (int i = 0; i < 10; i++)
        {
            await _boltCaller.InvokeAsync("tp_svc", "echo", _boltPayload);
            await _grpcClient.EchoPayloadAsync(_grpcRequest);
        }

        if (_boltQuicCaller != null)
        {
            for (int i = 0; i < 10; i++)
                await _boltQuicCaller.InvokeAsync("quic_tp_svc", "echo", _boltPayload);
        }
    }

    private async Task SetupBolt()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://localhost:18610");
        builder.Services.AddSingleton<BoltServer>();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        _boltApp = builder.Build();
        _boltApp.UseWebSockets();
        _boltApp.MapBolt("/bolt");
        _boltApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _boltApp.RunAsync());
        await WaitForHealth("http://localhost:18610/health");

        var lf = _boltApp.Services.GetRequiredService<ILoggerFactory>();
        var opts = new BoltClientOptions { RpcTimeoutSeconds = 60 };

        _boltService = new BoltClient(new Uri("ws://localhost:18610/bolt"),
            "tp_svc", "ThroughputSvc", opts, lf.CreateLogger<BoltClient>());
        _boltService.RegisterHandler("echo", (payload, _) =>
            Task.FromResult((HttpStatusCode.OK, payload)));
        await _boltService.ConnectAsync();

        _boltCaller = new BoltClient(new Uri("ws://localhost:18610/bolt"),
            "tp_caller", "ThroughputCaller", opts, lf.CreateLogger<BoltClient>());
        await _boltCaller.ConnectAsync();

        // QUIC transport (only when platform supports it)
        if (QuicConnection.IsSupported && QuicListener.IsSupported)
        {
            try
            {
                var server = _boltApp.Services.GetRequiredService<BoltServer>();
                _quicCert = GenerateSelfSignedCert();
                _quicListenerCts = new CancellationTokenSource();
                await StartQuicListenerAsync(server, 18613, _quicCert, _quicListenerCts.Token);

                var quicOpts = new BoltClientOptions
                {
                    RpcTimeoutSeconds = 60,
                    TransportAttemptTimeoutMs = 10_000,
                    PreferredTransports = [BoltTransport.Quic]
                };

                _boltQuicService = new BoltClient(new Uri("quic://127.0.0.1:18613/bolt"),
                    "quic_tp_svc", "QuicThroughputSvc", quicOpts, lf.CreateLogger<BoltClient>());
                _boltQuicService.RegisterHandler("echo", (payload, _) =>
                    Task.FromResult((HttpStatusCode.OK, payload)));
                await _boltQuicService.ConnectAsync();

                _boltQuicCaller = new BoltClient(new Uri("quic://127.0.0.1:18613/bolt"),
                    "quic_tp_caller", "QuicThroughputCaller", quicOpts, lf.CreateLogger<BoltClient>());
                await _boltQuicCaller.ConnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"QUIC setup failed: {ex.Message}");
                _boltQuicService = null;
                _boltQuicCaller = null;
            }
        }
    }

    private async Task SetupGrpc()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o =>
        {
            o.ListenLocalhost(18611, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(18612, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        builder.Services.AddGrpc(o => o.MaxReceiveMessageSize = 64 * 1024 * 1024);
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        _grpcApp = builder.Build();
        _grpcApp.MapGrpcService<GrpcEchoPayloadBackend>();
        _grpcApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _grpcApp.RunAsync());
        await WaitForHealth("http://localhost:18612/health");

        _grpcChannel = GrpcChannel.ForAddress("http://localhost:18611", new GrpcChannelOptions
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

    [Benchmark(Baseline = true, OperationsPerInvoke = OpsPerInvocation)]
    public async Task Bolt_WebSocket_Throughput()
    {
        for (int i = 0; i < OpsPerInvocation; i++)
            await _boltCaller.InvokeAsync("tp_svc", "echo", _boltPayload);
    }

    [Benchmark(OperationsPerInvoke = OpsPerInvocation)]
    public async Task Bolt_Quic_Throughput()
    {
        if (_boltQuicCaller == null)
        {
            for (int i = 0; i < OpsPerInvocation; i++)
                await Task.CompletedTask;
            return;
        }

        for (int i = 0; i < OpsPerInvocation; i++)
            await _boltQuicCaller.InvokeAsync("quic_tp_svc", "echo", _boltPayload);
    }

    [Benchmark(OperationsPerInvoke = OpsPerInvocation)]
    public async Task GRPC_Throughput()
    {
        for (int i = 0; i < OpsPerInvocation; i++)
            await _grpcClient.EchoPayloadAsync(_grpcRequest);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        try { if (_boltQuicCaller != null) await _boltQuicCaller.DisposeAsync(); } catch { }
        try { if (_boltQuicService != null) await _boltQuicService.DisposeAsync(); } catch { }
        _quicListenerCts?.Cancel();
        _quicCert?.Dispose();
        try { await _boltCaller.DisposeAsync(); } catch { }
        try { await _boltService.DisposeAsync(); } catch { }
        _grpcChannel?.Dispose();
        try { await _grpcApp.StopAsync(); } catch { }
        try { await _boltApp.StopAsync(); } catch { }
    }

    private static async Task StartQuicListenerAsync(BoltServer server, int port, X509Certificate2 cert, CancellationToken ct)
    {
        if (!QuicListener.IsSupported) return;

        var listener = await QuicListener.ListenAsync(new QuicListenerOptions
        {
            ListenEndPoint = new IPEndPoint(IPAddress.Loopback, port),
            ApplicationProtocols = [new SslApplicationProtocol("bolt")],
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions
            {
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 0,
                MaxInboundBidirectionalStreams = 256,
                    MaxInboundUnidirectionalStreams = 256,
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ServerCertificate = cert,
                    ApplicationProtocols = [new SslApplicationProtocol("bolt")]
                }
            })
        }, ct);

        _ = Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var quicConn = await listener.AcceptConnectionAsync(ct);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var transport = new QuicBoltConnection(quicConn);
                            transport.StartAcceptLoop(ct);
                            await server.HandleConnectionAsync(transport, ct);
                        }
                        catch { }
                    }, ct);
                }
            }
            catch (OperationCanceledException) { }
            finally { await listener.DisposeAsync(); }
        }, ct);
    }

    private static X509Certificate2 GenerateSelfSignedCert()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));
        return new X509Certificate2(cert.Export(X509ContentType.Pfx));
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

public class ThroughputBenchConfig : ManualConfig
{
    public ThroughputBenchConfig()
    {
        AddJob(Job.ShortRun.WithWarmupCount(3).WithIterationCount(10));
        AddColumn(StatisticColumn.P95);
        AddColumn(StatisticColumn.OperationsPerSecond);
        WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Microsecond));
    }
}

/// <summary>
/// Concurrency benchmark: Bolt (WebSocket) vs Bolt (QUIC) vs gRPC.
/// Fires N parallel RPCs simultaneously to measure P95 latency and stability
/// under contention.
///
/// Fixed payload: 1KB (typical RPC message).
/// Concurrency levels: 10, 50, 100, 500 parallel tasks.
///
/// Each benchmark invocation fires all RPCs concurrently with Task.WhenAll.
/// BenchmarkDotNet captures the distribution (mean, P95, stddev) of the
/// total time to complete all parallel RPCs.
/// </summary>
[Config(typeof(ConcurrencyBenchConfig))]
[MemoryDiagnoser]
public class ConcurrencyBenchmarks
{
    private const int PayloadSize = 1024;

    // Bolt (WebSocket)
    private WebApplication _boltApp = null!;
    private BoltClient _boltService = null!;
    private BoltClient _boltCaller = null!;
    private byte[] _boltPayload = null!;

    // Bolt (QUIC)
    private BoltClient? _boltQuicService;
    private BoltClient? _boltQuicCaller;
    private X509Certificate2? _quicCert;
    private CancellationTokenSource? _quicListenerCts;

    // gRPC
    private WebApplication _grpcApp = null!;
    private GrpcChannel _grpcChannel = null!;
    private HelloService.HelloServiceClient _grpcClient = null!;
    private PayloadRequest _grpcRequest = null!;

    [Params(10, 50, 100, 500)]
    public int Concurrency { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        await SetupBolt();
        await SetupGrpc();
        GeneratePayloads();

        // Warmup all transports
        for (int i = 0; i < 10; i++)
        {
            await _boltCaller.InvokeAsync("cc_svc", "echo", _boltPayload);
            await _grpcClient.EchoPayloadAsync(_grpcRequest);
        }

        if (_boltQuicCaller != null)
        {
            for (int i = 0; i < 10; i++)
                await _boltQuicCaller.InvokeAsync("quic_cc_svc", "echo", _boltPayload);
        }
    }

    private async Task SetupBolt()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://localhost:18620");
        builder.Services.AddSingleton<BoltServer>();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        _boltApp = builder.Build();
        _boltApp.UseWebSockets();
        _boltApp.MapBolt("/bolt");
        _boltApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _boltApp.RunAsync());
        await WaitForHealth("http://localhost:18620/health");

        var lf = _boltApp.Services.GetRequiredService<ILoggerFactory>();
        var opts = new BoltClientOptions { RpcTimeoutSeconds = 60 };

        _boltService = new BoltClient(new Uri("ws://localhost:18620/bolt"),
            "cc_svc", "ConcurrencySvc", opts, lf.CreateLogger<BoltClient>());
        _boltService.RegisterHandler("echo", (payload, _) =>
            Task.FromResult((HttpStatusCode.OK, payload)));
        await _boltService.ConnectAsync();

        _boltCaller = new BoltClient(new Uri("ws://localhost:18620/bolt"),
            "cc_caller", "ConcurrencyCaller", opts, lf.CreateLogger<BoltClient>());
        await _boltCaller.ConnectAsync();

        // QUIC transport (only when platform supports it)
        if (QuicConnection.IsSupported && QuicListener.IsSupported)
        {
            try
            {
                var server = _boltApp.Services.GetRequiredService<BoltServer>();
                _quicCert = GenerateSelfSignedCert();
                _quicListenerCts = new CancellationTokenSource();
                await StartQuicListenerAsync(server, 18623, _quicCert, _quicListenerCts.Token);

                var quicOpts = new BoltClientOptions
                {
                    RpcTimeoutSeconds = 60,
                    TransportAttemptTimeoutMs = 10_000,
                    PreferredTransports = [BoltTransport.Quic]
                };

                _boltQuicService = new BoltClient(new Uri("quic://127.0.0.1:18623/bolt"),
                    "quic_cc_svc", "QuicConcurrencySvc", quicOpts, lf.CreateLogger<BoltClient>());
                _boltQuicService.RegisterHandler("echo", (payload, _) =>
                    Task.FromResult((HttpStatusCode.OK, payload)));
                await _boltQuicService.ConnectAsync();

                _boltQuicCaller = new BoltClient(new Uri("quic://127.0.0.1:18623/bolt"),
                    "quic_cc_caller", "QuicConcurrencyCaller", quicOpts, lf.CreateLogger<BoltClient>());
                await _boltQuicCaller.ConnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"QUIC setup failed: {ex.Message}");
                _boltQuicService = null;
                _boltQuicCaller = null;
            }
        }
    }

    private async Task SetupGrpc()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o =>
        {
            o.ListenLocalhost(18621, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            o.ListenLocalhost(18622, lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
        builder.Services.AddGrpc(o => o.MaxReceiveMessageSize = 64 * 1024 * 1024);
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        _grpcApp = builder.Build();
        _grpcApp.MapGrpcService<GrpcEchoPayloadBackend>();
        _grpcApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _grpcApp.RunAsync());
        await WaitForHealth("http://localhost:18622/health");

        _grpcChannel = GrpcChannel.ForAddress("http://localhost:18621", new GrpcChannelOptions
        {
            MaxReceiveMessageSize = 64 * 1024 * 1024,
            MaxSendMessageSize = 64 * 1024 * 1024
        });
        _grpcClient = new HelloService.HelloServiceClient(_grpcChannel);
    }

    private void GeneratePayloads()
    {
        var data = new byte[PayloadSize];
        Random.Shared.NextBytes(data);

        _boltPayload = MemoryPackSerializer.Serialize(new BenchPayload { Data = data });
        _grpcRequest = new PayloadRequest { Data = ByteString.CopyFrom(data) };
    }

    [Benchmark(Baseline = true)]
    public async Task Bolt_WebSocket_Concurrent()
    {
        var tasks = new Task[Concurrency];
        for (int i = 0; i < Concurrency; i++)
            tasks[i] = _boltCaller.InvokeAsync("cc_svc", "echo", _boltPayload);
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task Bolt_Quic_Concurrent()
    {
        if (_boltQuicCaller == null)
        {
            await Task.CompletedTask;
            return;
        }

        var tasks = new Task[Concurrency];
        for (int i = 0; i < Concurrency; i++)
            tasks[i] = _boltQuicCaller.InvokeAsync("quic_cc_svc", "echo", _boltPayload);
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task GRPC_Concurrent()
    {
        var tasks = new Task[Concurrency];
        for (int i = 0; i < Concurrency; i++)
            tasks[i] = _grpcClient.EchoPayloadAsync(_grpcRequest).ResponseAsync;
        await Task.WhenAll(tasks);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        try { if (_boltQuicCaller != null) await _boltQuicCaller.DisposeAsync(); } catch { }
        try { if (_boltQuicService != null) await _boltQuicService.DisposeAsync(); } catch { }
        _quicListenerCts?.Cancel();
        _quicCert?.Dispose();
        try { await _boltCaller.DisposeAsync(); } catch { }
        try { await _boltService.DisposeAsync(); } catch { }
        _grpcChannel?.Dispose();
        try { await _grpcApp.StopAsync(); } catch { }
        try { await _boltApp.StopAsync(); } catch { }
    }

    private static async Task StartQuicListenerAsync(BoltServer server, int port, X509Certificate2 cert, CancellationToken ct)
    {
        if (!QuicListener.IsSupported) return;

        var listener = await QuicListener.ListenAsync(new QuicListenerOptions
        {
            ListenEndPoint = new IPEndPoint(IPAddress.Loopback, port),
            ApplicationProtocols = [new SslApplicationProtocol("bolt")],
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions
            {
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 0,
                MaxInboundBidirectionalStreams = 256,
                    MaxInboundUnidirectionalStreams = 256,
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ServerCertificate = cert,
                    ApplicationProtocols = [new SslApplicationProtocol("bolt")]
                }
            })
        }, ct);

        _ = Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var quicConn = await listener.AcceptConnectionAsync(ct);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var transport = new QuicBoltConnection(quicConn);
                            transport.StartAcceptLoop(ct);
                            await server.HandleConnectionAsync(transport, ct);
                        }
                        catch { }
                    }, ct);
                }
            }
            catch (OperationCanceledException) { }
            finally { await listener.DisposeAsync(); }
        }, ct);
    }

    private static X509Certificate2 GenerateSelfSignedCert()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));
        return new X509Certificate2(cert.Export(X509ContentType.Pfx));
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

public class ConcurrencyBenchConfig : ManualConfig
{
    public ConcurrencyBenchConfig()
    {
        AddJob(Job.ShortRun.WithWarmupCount(3).WithIterationCount(10));
        AddColumn(StatisticColumn.P95);
        WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Millisecond));
    }
}
