using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Bolt.Client;
using Bolt.Client.Transport;
using Bolt.Protocol.Transport;
using Bolt.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public class WebSocketBoltConnectionTests
{
    [Test]
    public void TransportType_IsWebSocket()
    {
        using var ws = new ClientWebSocket();
        var conn = new WebSocketBoltConnection(ws);
        conn.TransportType.Should().Be(BoltTransport.WebSocket);
    }

    [Test]
    public void SupportsDatagrams_IsFalse()
    {
        using var ws = new ClientWebSocket();
        var conn = new WebSocketBoltConnection(ws);
        conn.SupportsDatagrams.Should().BeFalse();
    }

    [Test]
    public async Task SendDatagramAsync_IsNoOp()
    {
        using var ws = new ClientWebSocket();
        var conn = new WebSocketBoltConnection(ws);
        await conn.SendDatagramAsync(new byte[] { 1, 2, 3 });
    }
}

[TestFixture]
public class QuicFramingTests
{
    [Test]
    public void WriteLengthPrefix_ReadsBackCorrectly()
    {
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var framed = new byte[4 + payload.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(framed, (uint)payload.Length);
        payload.CopyTo(framed.AsSpan(4));

        var readLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(framed);
        readLen.Should().Be(5);
        framed.AsSpan(4, (int)readLen).ToArray().Should().Equal(payload);
    }

    [Test]
    public void WriteLengthPrefix_LargePayload_CorrectLength()
    {
        var payload = new byte[1_048_576];
        Random.Shared.NextBytes(payload);
        var framed = new byte[4 + payload.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(framed, (uint)payload.Length);
        payload.CopyTo(framed.AsSpan(4));

        var readLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(framed);
        readLen.Should().Be(1_048_576);
    }
}

[TestFixture]
public class QuicTransportIntegrationTests
{
    private WebApplication _hubApp = null!;
    private BoltServer _server = null!;
    private CancellationTokenSource _quicListenerCts = null!;
    private QuicListener? _quicListener;
    private X509Certificate2 _cert = null!;
    private const int WsPort = 18700;
    private const int QuicPort = 18701;
    private static bool _quicSupported;
    private static bool _quicListenerReady;

    [OneTimeSetUp]
    public async Task Setup()
    {
        _quicSupported = QuicListener.IsSupported && QuicConnection.IsSupported;
        if (!_quicSupported)
            return;

        _cert = GenerateSelfSignedCert();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{WsPort}");
        builder.Services.AddBoltServer();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        _hubApp = builder.Build();
        _hubApp.UseWebSockets();
        _hubApp.MapBolt("/bolt");
        _hubApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _hubApp.RunAsync());
        await WaitForHealth($"http://localhost:{WsPort}/health");

        _server = _hubApp.Services.GetRequiredService<BoltServer>();

        // Start QUIC listener in test (avoids circular dependency between Bolt.Server and Bolt.Client)
        _quicListenerCts = new CancellationTokenSource();
        try
        {
            await StartQuicListenerAsync();
            _quicListenerReady = true;
        }
        catch (Exception ex)
        {
            // QUIC API reports supported but fails at runtime (e.g., missing TLS 1.3, OS config)
            TestContext.Progress.WriteLine($"QUIC listener start failed — will skip QUIC tests: {ex.Message}");
            _quicListenerReady = false;
        }
    }

    private async Task StartQuicListenerAsync()
    {
        _quicListener = await QuicListener.ListenAsync(new QuicListenerOptions
        {
            ListenEndPoint = new IPEndPoint(IPAddress.Loopback, QuicPort),
            ApplicationProtocols = [new SslApplicationProtocol("bolt")],
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions
            {
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 0,
                MaxInboundBidirectionalStreams = 256,
                    MaxInboundUnidirectionalStreams = 256,
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ServerCertificate = _cert,
                    ApplicationProtocols = [new SslApplicationProtocol("bolt")]
                }
            })
        }, _quicListenerCts.Token);

        _ = Task.Run(async () =>
        {
            try
            {
                while (!_quicListenerCts.Token.IsCancellationRequested)
                {
                    var quicConn = await _quicListener.AcceptConnectionAsync(_quicListenerCts.Token);
                    // Handle each connection asynchronously so the accept loop continues
                    _ = HandleQuicConnectionAsync(quicConn, _quicListenerCts.Token);
                }
            }
            catch (OperationCanceledException) { }
            finally { await _quicListener.DisposeAsync(); }
        });
    }

    private async Task HandleQuicConnectionAsync(QuicConnection quicConn, CancellationToken ct)
    {
        try
        {
            var transport = new QuicBoltConnection(quicConn);
            transport.StartAcceptLoop(ct);
            await _server.HandleConnectionAsync(transport, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Individual connection failure should not crash the accept loop
        }
    }

    private static void SkipIfQuicUnavailable()
    {
        if (!_quicSupported || !_quicListenerReady)
            Assert.Ignore($"QUIC not available on this platform (IsSupported={_quicSupported}, ListenerReady={_quicListenerReady})");
    }

    [Test]
    public async Task Quic_RpcCall_WorksThroughHub()
    {
        SkipIfQuicUnavailable();

        var lf = _hubApp.Services.GetRequiredService<ILoggerFactory>();
        var quicOpts = new BoltClientOptions
        {
            RpcTimeoutSeconds = 10,
            TransportAttemptTimeoutMs = 10_000,
            PreferredTransports = [BoltTransport.Quic]
        };

        // Service registers an echo handler on QUIC
        var service = new BoltClient(
            new Uri($"quic://127.0.0.1:{QuicPort}/bolt"),
            "quic_svc", "QuicSvc", quicOpts, lf.CreateLogger<BoltClient>());
        service.RegisterHandler("echo", (payload, _requestId) =>
            Task.FromResult((HttpStatusCode.OK, payload)));
        await service.ConnectAsync();

        // Caller sends RPC through the hub to the service
        var caller = new BoltClient(
            new Uri($"quic://127.0.0.1:{QuicPort}/bolt"),
            "quic_caller", "QuicCaller", quicOpts, lf.CreateLogger<BoltClient>());
        await caller.ConnectAsync();

        var testData = new byte[1024];
        Random.Shared.NextBytes(testData);
        var (status, response) = await caller.InvokeAsync("quic_svc", "echo", testData);

        status.Should().Be(HttpStatusCode.OK);
        response.ToArray().Should().Equal(testData);

        await caller.DisposeAsync();
        await service.DisposeAsync();
    }

    [Test]
    public async Task MixedTransport_QuicCaller_WebSocketService()
    {
        SkipIfQuicUnavailable();

        var lf = _hubApp.Services.GetRequiredService<ILoggerFactory>();

        // Service connects via WebSocket
        var wsOpts = new BoltClientOptions
        {
            RpcTimeoutSeconds = 10,
            PreferredTransports = [BoltTransport.WebSocket]
        };
        var service = new BoltClient(
            new Uri($"ws://localhost:{WsPort}/bolt"),
            "ws_svc_mix", "WsSvcMix", wsOpts, lf.CreateLogger<BoltClient>());
        service.RegisterHandler("echo", (payload, _requestId) =>
            Task.FromResult((HttpStatusCode.OK, payload)));
        await service.ConnectAsync();

        // Caller connects via QUIC
        var quicOpts = new BoltClientOptions
        {
            RpcTimeoutSeconds = 10,
            TransportAttemptTimeoutMs = 10_000,
            PreferredTransports = [BoltTransport.Quic]
        };
        var caller = new BoltClient(
            new Uri($"quic://127.0.0.1:{QuicPort}/bolt"),
            "quic_caller_mix", "QuicCallerMix", quicOpts, lf.CreateLogger<BoltClient>());
        await caller.ConnectAsync();

        // QUIC caller invokes echo on WebSocket service — mixed transport through hub
        var testData = new byte[512];
        Random.Shared.NextBytes(testData);
        var (status, response) = await caller.InvokeAsync("ws_svc_mix", "echo", testData);

        status.Should().Be(HttpStatusCode.OK);
        response.ToArray().Should().Equal(testData);

        await caller.DisposeAsync();
        await service.DisposeAsync();
    }

    [Test]
    public async Task TransportFallback_QuicUnavailable_FallsBackToWebSocket()
    {
        // This test only needs the WebSocket hub, not QUIC
        if (!_quicSupported)
        {
            // Even without QUIC, the fallback test is valid — QUIC will be skipped,
            // WebSocket is the only transport that works
        }

        var lf = _hubApp!.Services.GetRequiredService<ILoggerFactory>();
        var opts = new BoltClientOptions
        {
            RpcTimeoutSeconds = 10,
            TransportAttemptTimeoutMs = 1000,
            // Try QUIC first (will fail — no QUIC listener on WsPort), then WebSocket
            PreferredTransports = [BoltTransport.Quic, BoltTransport.WebSocket]
        };

        // Connect to the WebSocket-only port — QUIC attempt fails, WebSocket succeeds
        var client = new BoltClient(
            new Uri($"ws://localhost:{WsPort}/bolt"),
            "fallback_client", "FallbackClient", opts, lf.CreateLogger<BoltClient>());
        await client.ConnectAsync();

        client.IsConnected.Should().BeTrue();

        await client.DisposeAsync();
    }

    [OneTimeTearDown]
    public async Task Cleanup()
    {
        _quicListenerCts?.Cancel();
        try { if (_hubApp != null) await _hubApp.StopAsync(); } catch { }
        _cert?.Dispose();
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
            try
            {
                if ((await client.GetAsync(url)).IsSuccessStatusCode) return;
            }
            catch { }
            await Task.Delay(100);
        }
        throw new TimeoutException($"Service at {url} not healthy within {timeoutSeconds}s");
    }
}
