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
    private sealed class RelaySide(IRtcPeer peer) { public IRtcPeer Peer { get; } = peer; public int Received; }
    private readonly ConcurrentDictionary<string, RelaySide> _relays = new();

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
        var relay = _relays[id] = new RelaySide(peer);
        peer.LocalCandidate += candidate => _ = Send(new { type = "candidate", candidate = candidate.Candidate, sdpMid = candidate.SdpMid, sdpMLineIndex = candidate.SdpMLineIndex });
        peer.Message += data => { Interlocked.Increment(ref relay.Received); peer.TrySend(data.Span); };
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
          const result = { states: [], path: null, echoed: 0, opened: false, supported: typeof RTCPeerConnection === 'function', bad: 0, candidates: [] };
          let opened; const open = new Promise(resolve => opened = resolve);
          const seen = new Set();
          const dotnet = { invokeMethodAsync: async (method, ...args) => {
            if (method === 'OnCandidate') {
              ws.send(JSON.stringify({ type: 'candidate', candidate: args[0], sdpMid: args[1], sdpMLineIndex: args[2] }));
              // Only each candidate's transport and type, for a failure message.
              const parts = (args[0] || '').split(' ');
              if (parts.length > 7) result.candidates.push(parts[2] + '/' + parts[7]);
            }
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
          return { States: result.states, Path: path, Echoed: result.echoed, Opened: result.opened, Supported: result.supported, Bad: result.bad,
            Candidates: result.candidates };
        };
        // A sender at video rates, paced the way the call's pacer is (send while the buffer is under 48 KB): how much
        // arrives, whether the buffer drains once sending stops, and whether it ever sat full without draining for
        // two seconds (which the client and relay read as a stalled channel).
        window.runRate = async ({ id, iceServers, kbps, seconds, timeoutMs }) => {
          const ws = new WebSocket(`ws://${location.host}/signal?id=${id}`);
          await new Promise((resolve, reject) => { ws.onopen = resolve; ws.onerror = reject; });
          let opened; const open = new Promise(resolve => opened = resolve);
          let echoed = 0;
          const dotnet = { invokeMethodAsync: async (method, ...args) => {
            if (method === 'OnCandidate') ws.send(JSON.stringify({ type: 'candidate', candidate: args[0], sdpMid: args[1], sdpMLineIndex: args[2] }));
            else if (method === 'OnState' && args[0] === 'open') opened();
            else if (method === 'OnMessage') echoed++;
          } };
          const peer = createPeer(dotnet, { iceServers, iceTransportPolicy: 'relay', maxMessageBytes: 1150 });
          ws.onmessage = async event => {
            const message = JSON.parse(event.data);
            if (message.type === 'answer') await peer.setAnswer(message.sdp);
            else if (message.type === 'candidate') await peer.addCandidate(message.candidate, message.sdpMid, message.sdpMLineIndex);
          };
          ws.send(JSON.stringify({ type: 'offer', sdp: await peer.createOffer(false) }));
          await Promise.race([open, new Promise(resolve => setTimeout(resolve, timeoutMs))]);
          const result = { Opened: peer.state() === 'open', Sent: 0, Echoed: 0, MaxBuffered: 0, StuckMs: 0, DrainedAfterStop: false, Held: 0 };
          if (result.Opened) {
            const message = new Uint8Array(1150); message[0] = 0x21;
            const perTick = kbps * 1000 / 8 * 0.005;
            let credit = 0, lastBuffered = 0, added = 0, lastProgress = performance.now();
            const started = performance.now();
            let nextSample = started + 250;
            while (performance.now() - started < seconds * 1000) {
              await new Promise(resolve => setTimeout(resolve, 5));
              credit = Math.min(credit + perTick, 64 * 1024);
              while (credit >= message.length) {
                if (peer.bufferedAmount() > 48 * 1024) { result.Held++; break; }
                if (!peer.send(message)) break;
                result.Sent++; added += message.length; credit -= message.length;
              }
              const now = performance.now();
              if (now >= nextSample) {
                nextSample = now + 250;
                const buffered = peer.bufferedAmount();
                result.MaxBuffered = Math.max(result.MaxBuffered, buffered);
                if (buffered <= 0 || lastBuffered + added - buffered > 0) lastProgress = now;
                result.StuckMs = Math.max(result.StuckMs, now - lastProgress);
                lastBuffered = buffered; added = 0;
              }
            }
            const drainDeadline = performance.now() + 3000;
            while (peer.bufferedAmount() > 0 && performance.now() < drainDeadline) await new Promise(resolve => setTimeout(resolve, 20));
            result.DrainedAfterStop = peer.bufferedAmount() === 0;
            await new Promise(resolve => setTimeout(resolve, 1000));
            result.Echoed = echoed;
          }
          ws.send(JSON.stringify({ type: 'done' }));
          peer.close(); ws.close();
          return result;
        };
        window.ready = true;
        </script>
        """;

    private sealed record CallResult(string[] States, BrowserPath? Path, int Echoed, bool Opened, bool Supported, int Bad, string[]? Candidates = null)
    {
        public override string ToString() => $"states [{string.Join(",", States)}], candidates [{string.Join(",", Candidates ?? [])}]";
    }
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
                    Args = ["--disable-features=WebRtcHideLocalIpsWithMdns", "--allow-loopback-in-peer-connection"],
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
        // As JSON: Playwright's own conversion cannot build positional records.
        var json = await page.EvaluateAsync<string>("options => window.runCall(options).then(result => JSON.stringify(result))", new
        {
            id,
            iceServers = browserServers.Select(x => new { urls = x.Urls, username = x.Username, credential = x.Credential }),
            policy = "relay",
            count,
            timeoutMs,
        });
        var result = JsonSerializer.Deserialize<CallResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        if (!_relays.TryGetValue(id, out var relay)) return (result, null);
        // The relay's view of the same call: its path and SCTP window arrive with its periodic reports.
        for (var wait = 0; result.Opened && wait < 50 && (relay.Peer.Path is null || relay.Peer.CongestionWindow == 0); wait++)
            await Task.Delay(100);
        return (result, new ServerResult(relay.Peer.Path?.Describe(), relay.Peer.CongestionWindow, Volatile.Read(ref relay.Received)));
    }

    [TestCase("chromium")]
    [TestCase("webkit")]
    public async Task ThroughTurnOverUdp_TheChannelOpens_AndCarriesFullSizeMessagesBothWays(string engine)
    {
        var (browser, relay) = await CallAsync(engine, [Turn($"turn:{_turnHost}:3478?transport=udp", "browser")]);
        if (!browser.Supported) Assert.Ignore($"{engine} has no RTCPeerConnection here.");
        Assert.Multiple(() =>
        {
            Assert.That(browser.Opened, Is.True, browser.ToString());
            Assert.That(browser.Echoed, Is.EqualTo(200), "every 1150-byte message makes the round trip on a clean path");
            Assert.That(browser.Bad, Is.Zero, "no message is cut, merged or duplicated");
            Assert.That(browser.Path?.Local, Is.EqualTo("relay"));
            Assert.That(browser.Path?.RelayProtocol ?? "udp", Is.EqualTo("udp"));
            Assert.That(relay?.Path, Is.EqualTo("UDP/relay"), "the relay's side allocates on TURN over UDP");
            Assert.That(relay?.Cwnd, Is.GreaterThanOrEqualTo(128 * 1024), "the SCTP window floor is in force");
            Assert.That(relay?.Received, Is.EqualTo(200));
        });
    }

    private sealed record RateResult(bool Opened, int Sent, int Echoed, double MaxBuffered, double StuckMs, bool DrainedAfterStop, int Held);

    /// <summary>
    /// The phone's uplink at video rates, through TURN, paced as the call's pacer paces it. The pacer counts the
    /// channel's buffered amount as its own backlog and the client treats a buffer that drains nothing for two seconds
    /// as a stalled channel, so both must hold in a real browser: the buffer drains while sending, and empties after.
    /// The workflow runs this once on a clean loopback and once under netem (delay and loss).
    /// </summary>
    [TestCase("chromium")]
    [TestCase("webkit")]
    public async Task AtVideoRates_TheBrowserUplinkDelivers_AndItsBufferKeepsDraining(string engine)
    {
        IBrowser browser;
        try
        {
            browser = engine == "webkit"
                ? await _playwright!.Webkit.LaunchAsync(new() { Headless = true })
                : await _playwright!.Chromium.LaunchAsync(new()
                {
                    Headless = true, ExecutablePath = Env("CHROME_PATH"),
                    Args = ["--disable-features=WebRtcHideLocalIpsWithMdns", "--allow-loopback-in-peer-connection"],
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
        var json = await page.EvaluateAsync<string>("options => window.runRate(options).then(result => JSON.stringify(result))", new
        {
            id,
            iceServers = new[] { Turn($"turn:{_turnHost}:3478?transport=udp", "browser") }.Select(x => new { urls = x.Urls, username = x.Username, credential = x.Credential }),
            kbps = 2000,
            seconds = 8,
            timeoutMs = 15000,
        });
        var result = JsonSerializer.Deserialize<RateResult>(json)!;
        if (!result.Opened) Assert.Ignore($"{engine}: the channel did not open here ({json}).");
        var received = _relays.TryGetValue(id, out var relay) ? Volatile.Read(ref relay.Received) : 0;
        TestContext.Out.WriteLine($"{engine}: {json} relayReceived={received}");
        Assert.Multiple(() =>
        {
            Assert.That(result.Sent, Is.GreaterThan(1500), "the pacer kept sending at about 2 Mbit/s");
            Assert.That(received, Is.GreaterThanOrEqualTo(result.Sent * 9 / 10), "what the browser sent reached the relay");
            Assert.That(result.StuckMs, Is.LessThan(2000), "a working channel never sits full without draining for the stall window");
            Assert.That(result.DrainedAfterStop, Is.True, "the buffered amount empties once sending stops");
        });
    }

    [Test]
    public async Task WhenOnlyTcpReachesTurn_TheBrowserStillGetsAChannel_OverTurnTcp()
    {
        var (browser, relay) = await CallAsync("chromium", [Turn($"turn:{_turnHost}:3478?transport=tcp", "browser")]);
        Assert.Multiple(() =>
        {
            Assert.That(browser.Opened, Is.True, browser.ToString());
            // The channel never retransmits, and coturn re-sends the TCP leg's burst as UDP to the relay, where a
            // few datagrams can drop on a busy runner. The test proves the path, not a lossless one.
            Assert.That(browser.Echoed, Is.InRange(190, 200));
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
