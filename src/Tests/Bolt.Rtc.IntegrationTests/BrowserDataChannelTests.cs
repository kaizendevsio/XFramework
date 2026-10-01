using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bolt.Protocol.Transport;
using Bolt.Rtc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Bolt.Rtc.IntegrationTests;

/// <summary>
/// A real browser's RTCPeerConnection (through the production bolt-rtc.js) against the relay's real WebRTC
/// endpoint (the bolt-rtc sidecar through <see cref="RtcSidecar"/>), both forced through a real TURN server.
/// The relay side is relay-only, as in production; the browser side is relay-only too here, so the path
/// provably runs through TURN. Each test sends a burst of maximum-size messages and expects them echoed.
/// </summary>
[TestFixture]
[NonParallelizable]
[CancelAfter(120_000)]
public sealed class BrowserDataChannelTests
{
    private static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;

    private string _sidecarPath = "";
    private string _turnHost = "";
    private string _turnSecret = "";
    private WebApplication? _app;
    private RtcSidecar? _sidecar;
    private IPlaywright? _playwright;
    private string _origin = "";
    private readonly ConcurrentQueue<IRtcPeer> _relayPeers = new();

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        if (Env("BOLT_RTC_SIDECAR") is not { } sidecar || Env("BOLT_RTC_TURN_HOST") is not { } host || Env("BOLT_RTC_TURN_SECRET") is not { } secret)
        {
            Assert.Ignore("Set BOLT_RTC_SIDECAR, BOLT_RTC_TURN_HOST and BOLT_RTC_TURN_SECRET (see call-udp-integration.yml).");
            return;
        }
        (_sidecarPath, _turnHost, _turnSecret) = (sidecar, host, secret);
        _sidecar = new RtcSidecar(new RtcSidecarOptions { ExecutablePath = _sidecarPath });
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        var app = _app = builder.Build();
        app.UseWebSockets();
        app.MapGet("/", () => Results.Content(Page, "text/html"));
        app.MapGet("/bolt-rtc.js", () => Results.File(Path.Combine(AppContext.BaseDirectory, "bolt-rtc.js"), "text/javascript"));
        app.Map("/signal", SignalAsync);
        await app.StartAsync();
        _origin = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _playwright = await Playwright.CreateAsync();
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        while (_relayPeers.TryDequeue(out var peer)) await peer.DisposeAsync();
        if (_sidecar is not null) await _sidecar.DisposeAsync();
        if (_app is not null) await _app.DisposeAsync();
        _playwright?.Dispose();
    }

    /// <summary>TURN REST credentials (coturn use-auth-secret), as the relay derives them.</summary>
    private RtcIceServer Turn(string url, string label)
    {
        var username = $"{DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds()}:{label}";
        var credential = Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes(_turnSecret), Encoding.UTF8.GetBytes(username)));
        return new RtcIceServer([url], username, credential);
    }

    private sealed record ServerResult(string? Path, long Cwnd, int Received);
    private readonly ConcurrentDictionary<string, ServerResult> _serverResults = new();

    /// <summary>The relay's end: one answering peer per page, relay-only, echoing every message back.</summary>
    private async Task SignalAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
        var id = context.Request.Query["id"].ToString();
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var sendGate = new SemaphoreSlim(1, 1);
        async Task Send(object message)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(message);
            await sendGate.WaitAsync();
            try { if (socket.State == WebSocketState.Open) await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None); }
            finally { sendGate.Release(); }
        }
        var peer = await _sidecar!.CreateAsync(RtcPeerRole.Answer, new RtcPeerOptions(
            [Turn($"turn:{_turnHost}:3478?transport=udp", "relay")], RelayOnly: true, RtcDefaults.MaxMessageBytes, 128 * 1024), CancellationToken.None);
        _relayPeers.Enqueue(peer);
        var received = 0;
        peer.LocalCandidate += candidate => _ = Send(new { type = "candidate", candidate = candidate.Candidate, sdpMid = candidate.SdpMid, sdpMLineIndex = candidate.SdpMLineIndex });
        peer.Message += data => { Interlocked.Increment(ref received); peer.TrySend(data.Span); };
        peer.StateChanged += state => _serverResults[id] = new(peer.Path?.Describe(), peer.CongestionWindow, received);
        peer.PathChanged += path => _serverResults[id] = new(path.Describe(), peer.CongestionWindow, received);
        var buffer = new byte[64 * 1024];
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) break;
            using var message = JsonDocument.Parse(buffer.AsMemory(0, result.Count));
            var root = message.RootElement;
            switch (root.GetProperty("type").GetString())
            {
                case "offer":
                    var answer = await peer.AnswerAsync(root.GetProperty("sdp").GetString()!, CancellationToken.None);
                    await Send(new { type = "answer", sdp = answer });
                    break;
                case "candidate":
                    await peer.AddCandidateAsync(new RtcCandidate(root.GetProperty("candidate").GetString() ?? "",
                        root.TryGetProperty("sdpMid", out var mid) && mid.ValueKind == JsonValueKind.String ? mid.GetString() : null,
                        root.TryGetProperty("sdpMLineIndex", out var index) && index.ValueKind == JsonValueKind.Number ? index.GetInt32() : null), CancellationToken.None);
                    break;
                case "done":
                    _serverResults[id] = new(peer.Path?.Describe(), peer.CongestionWindow, received);
                    break;
            }
        }
    }

    private const string Page = """
        <!doctype html><meta charset="utf-8"><title>bolt-rtc</title>
        <script type="module">
        import { createPeer } from './bolt-rtc.js';
        window.runCall = async ({ id, iceServers, policy, count, timeoutMs }) => {
          const ws = new WebSocket(`ws://${location.host}/signal?id=${id}`);
          await new Promise((resolve, reject) => { ws.onopen = resolve; ws.onerror = reject; });
          const result = { states: [], path: null, echoed: 0, opened: false, supported: typeof RTCPeerConnection === 'function', bad: 0 };
          let opened; const open = new Promise(resolve => opened = resolve);
          const seen = new Set();
          const dotnet = { invokeMethodAsync: async (method, ...args) => {
            if (method === 'OnCandidate') ws.send(JSON.stringify({ type: 'candidate', candidate: args[0], sdpMid: args[1], sdpMLineIndex: args[2] }));
            else if (method === 'OnState') { result.states.push(args[0]); if (args[0] === 'open') opened(); }
            else if (method === 'OnPath') result.path = { local: args[0], localProtocol: args[1], relayProtocol: args[2], remote: args[3], rttMs: args[4] };
            else if (method === 'OnMessage') {
              const bytes = args[0];
              const index = bytes[1] | (bytes[2] << 8);
              if (bytes.length !== 1150 || bytes[0] !== 0x21 || seen.has(index)) result.bad++; else { seen.add(index); result.echoed++; }
            }
          } };
          const peer = createPeer(dotnet, { iceServers, iceTransportPolicy: policy, maxMessageBytes: 1150 });
          ws.onmessage = async event => {
            const message = JSON.parse(event.data);
            if (message.type === 'answer') await peer.setAnswer(message.sdp);
            else if (message.type === 'candidate') await peer.addCandidate(message.candidate, message.sdpMid, message.sdpMLineIndex);
          };
          ws.send(JSON.stringify({ type: 'offer', sdp: await peer.createOffer(false) }));
          const timer = new Promise(resolve => setTimeout(resolve, timeoutMs));
          await Promise.race([open, timer]);
          result.opened = result.states.includes('open');
          if (result.opened) {
            for (let i = 0; i < count; i++) {
              const message = new Uint8Array(1150); message[0] = 0x21; message[1] = i & 0xff; message[2] = i >> 8;
              while (peer.bufferedAmount() > 64 * 1024) await new Promise(resolve => setTimeout(resolve, 5));
              peer.send(message);
            }
            const deadline = Date.now() + 10000;
            while (result.echoed < count && Date.now() < deadline) await new Promise(resolve => setTimeout(resolve, 50));
            const pathDeadline = Date.now() + 5000;
            while (!result.path && Date.now() < pathDeadline) await new Promise(resolve => setTimeout(resolve, 100));
          }
          ws.send(JSON.stringify({ type: 'done' }));
          await new Promise(resolve => setTimeout(resolve, 300));
          peer.close(); ws.close();
          // Keys as the .NET record names them.
          const path = result.path && { Local: result.path.local, LocalProtocol: result.path.localProtocol,
            RelayProtocol: result.path.relayProtocol, Remote: result.path.remote, RttMs: result.path.rttMs };
          return { States: result.states, Path: path, Echoed: result.echoed, Opened: result.opened, Supported: result.supported, Bad: result.bad };
        };
        window.ready = true;
        </script>
        """;

    private sealed record CallResult(string[] States, BrowserPath? Path, int Echoed, bool Opened, bool Supported, int Bad);
    private sealed record BrowserPath(string Local, string LocalProtocol, string? RelayProtocol, string Remote, double RttMs);

    private async Task<(CallResult Browser, ServerResult? Relay)> CallAsync(string engine, RtcIceServer[] browserServers, int count = 200, int timeoutMs = 15000)
    {
        IBrowser browser;
        try
        {
            browser = engine == "webkit"
                ? await _playwright!.Webkit.LaunchAsync(new() { Headless = true })
                : await _playwright!.Chromium.LaunchAsync(new()
                {
                    Headless = true,
                    ExecutablePath = Env("CHROME_PATH"),
                    // Loopback TURN relays only: no host candidates are offered and none are needed.
                    Args = ["--disable-features=WebRtcHideLocalIpsWithMdns"],
                });
        }
        catch (PlaywrightException error)
        {
            Assert.Ignore($"{engine} is not installed here: {error.Message.Split('\n')[0]}");
            throw;
        }
        await using var _ = browser;
        var page = await browser.NewPageAsync();
        await page.GotoAsync(_origin + "/");
        await page.WaitForFunctionAsync("() => window.ready === true");
        var id = Guid.NewGuid().ToString("N");
        var result = await page.EvaluateAsync<CallResult>("options => window.runCall(options)", new
        {
            id,
            iceServers = browserServers.Select(x => new { urls = x.Urls, username = x.Username, credential = x.Credential }),
            policy = "relay",
            count,
            timeoutMs,
        });
        await Task.Delay(500);
        return (result, _serverResults.GetValueOrDefault(id));
    }

    [TestCase("chromium")]
    [TestCase("webkit")]
    public async Task ThroughTurnOverUdp_TheChannelOpens_AndCarriesFullSizeMessagesBothWays(string engine)
    {
        var (browser, relay) = await CallAsync(engine, [Turn($"turn:{_turnHost}:3478?transport=udp", "browser")]);
        if (!browser.Supported) Assert.Ignore($"{engine} has no RTCPeerConnection here.");
        Assert.Multiple(() =>
        {
            Assert.That(browser.Opened, Is.True, string.Join(",", browser.States));
            Assert.That(browser.Echoed, Is.EqualTo(200), "every 1150-byte message makes the round trip on a clean path");
            Assert.That(browser.Bad, Is.Zero, "no message is cut, merged or duplicated");
            Assert.That(browser.Path?.Local, Is.EqualTo("relay"));
            Assert.That(browser.Path?.RelayProtocol ?? "udp", Is.EqualTo("udp"));
            Assert.That(relay?.Path, Is.EqualTo("UDP/relay"), "the relay's side allocates on TURN over UDP");
            Assert.That(relay?.Cwnd, Is.GreaterThanOrEqualTo(128 * 1024), "the SCTP window floor is in force");
            Assert.That(relay?.Received, Is.EqualTo(200));
        });
    }

    [Test]
    public async Task WhenOnlyTcpReachesTurn_TheBrowserStillGetsAChannel_OverTurnTcp()
    {
        var (browser, relay) = await CallAsync("chromium", [Turn($"turn:{_turnHost}:3478?transport=tcp", "browser")]);
        Assert.Multiple(() =>
        {
            Assert.That(browser.Opened, Is.True, string.Join(",", browser.States));
            Assert.That(browser.Echoed, Is.EqualTo(200));
            Assert.That(browser.Path?.RelayProtocol, Is.EqualTo("tcp"), "the browser's leg rides TCP to TURN");
            Assert.That(relay?.Path, Is.EqualTo("UDP/relay"));
        });
    }

    [Test]
    public async Task WhenTurnIsUnreachable_TheChannelNeverOpens_SoTheCallStaysOnItsWebSocket()
    {
        // Nothing listens there: like a network that drops UDP, gathering finds no relay and ICE cannot connect.
        var (browser, relay) = await CallAsync("chromium", [Turn($"turn:{_turnHost}:3479?transport=udp", "browser")], timeoutMs: 8000);
        Assert.Multiple(() =>
        {
            Assert.That(browser.Opened, Is.False);
            Assert.That(browser.Echoed, Is.Zero);
            Assert.That(relay?.Received ?? 0, Is.Zero);
        });
    }
}
