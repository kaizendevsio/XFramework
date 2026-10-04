using System.Diagnostics;
using System.Text.Json;
using Bolt.Protocol.Transport;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Bolt.Rtc.IntegrationTests;

/// <summary>
/// The relay's own leg to TURN, degraded on its own (production, 04:09 UTC 2026-10-04: every relay allocation over UDP to
/// Cloudflare failed, "gathered map[]", and calls stayed on their WebSockets). The relay gets its own TURN server
/// (BOLT_RTC_RELAY_TURN_UDP_PORT / _TLS_PORT, a second coturn) so the workflow can drop or lose its UDP alone, while the
/// browser's leg goes through the first TURN server under netem. Each test runs only under the network condition it is
/// written for (BOLT_RTC_RELAY_CONDITION, set by the workflow step that degrades the network).
/// </summary>
public sealed partial class BrowserDataChannelTests
{
    private RtcSidecar? _singleFlowSidecar;
    private readonly Lock _sidecarLock = new();

    private static string? RelayCondition => Env("BOLT_RTC_RELAY_CONDITION");

    private RtcSidecar SingleFlowSidecar()
    {
        lock (_sidecarLock)
            return _singleFlowSidecar ??= new RtcSidecar(new RtcSidecarOptions { ExecutablePath = _sidecarPath, TurnFlows = 1 });
    }

    /// <summary>"udp"/"tcp": the shared TURN server; "relay-udp"/"relay-tls": the relay's own one.</summary>
    private string RelayUrl(string transport) => transport switch
    {
        "relay-udp" => $"turn:{_turnHost}:{Env("BOLT_RTC_RELAY_TURN_UDP_PORT")}?transport=udp",
        "relay-tls" => $"turns:{_turnHost}:{Env("BOLT_RTC_RELAY_TURN_TLS_PORT")}?transport=tcp",
        _ => $"turn:{_turnHost}:3478?transport={transport}",
    };

    private static void RequireCondition(string condition)
    {
        if (Env("BOLT_RTC_RELAY_TURN_UDP_PORT") is null || RelayCondition != condition)
            Assert.Ignore($"Runs in call-udp-integration.yml's '{condition}' step (BOLT_RTC_RELAY_CONDITION={condition}).");
    }

    /// <summary>What the relay sends in a downlink run: a steady rate plus a keyframe-sized burst every few seconds.</summary>
    private sealed record DownlinkPlan(int Kbps, int BurstKb, int EverySeconds, int Seconds)
    {
        public string Query => $"{Kbps}:{BurstKb}:{EverySeconds}:{Seconds}";

        public static DownlinkPlan? Parse(string? value) =>
            value?.Split(':') is [var kbps, var burst, var every, var seconds] && int.TryParse(kbps, out var k) && int.TryParse(burst, out var b) &&
            int.TryParse(every, out var e) && int.TryParse(seconds, out var s)
                ? new DownlinkPlan(k, b, e, s)
                : null;
    }

    private sealed class DownlinkStats
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly List<string> _states = [];
        public int Sent;
        public int Held;
        public int NotOpen;
        public double MaxStuckMs;
        public volatile bool Running;
        public volatile bool Finished;

        public void Note(RtcChannelState state)
        {
            lock (_states) _states.Add($"{_clock.ElapsedMilliseconds}ms {state}{(Running ? "" : Finished ? " (after)" : " (before)")}");
        }

        public string[] States { get { lock (_states) return [.. _states]; } }

        /// <summary>States the relay's channel went through while media was running: anything but open is a flap.</summary>
        public string[] Flaps { get { lock (_states) return [.. _states.Where(x => !x.EndsWith(")") && !x.EndsWith(" Open"))]; } }
    }

    /// <summary>
    /// The relay's side of a downlink run, fed the way the relay feeds a participant's channel: never more in flight than
    /// clamp(cwnd + 16 KiB, 32 KiB, 512 KiB), the rest held back (the relay's lanes would hold or drop it). While it runs,
    /// the buffered amount must keep draining: the relay's drain watchdog calls a channel that drains nothing for 2 s stalled.
    /// </summary>
    private static async Task RunDownlinkAsync(IRtcPeer peer, DownlinkStats stats, DownlinkPlan plan)
    {
        var message = new byte[RtcDefaults.MaxMessageBytes];
        message[0] = 0x21;
        var bytesPerMs = plan.Kbps / 8.0;
        var clock = Stopwatch.StartNew();
        double credit = 0, last = 0, nextBurst = 1000, nextSample = 100, lastProgress = 0;
        long lastBuffered = 0, added = 0;
        stats.Running = true;
        try
        {
            while (clock.ElapsedMilliseconds < plan.Seconds * 1000)
            {
                await Task.Delay(5);
                var now = clock.Elapsed.TotalMilliseconds;
                credit = Math.Min(credit + (now - last) * bytesPerMs, 64 * 1024 + plan.BurstKb * 1024);
                last = now;
                if (now >= nextBurst)
                {
                    credit += plan.BurstKb * 1024;
                    nextBurst += plan.EverySeconds * 1000;
                }
                var cap = Math.Clamp(peer.CongestionWindow + 16 * 1024, 32 * 1024, 512 * 1024);
                while (credit >= message.Length)
                {
                    if (peer.State != RtcChannelState.Open) { Interlocked.Increment(ref stats.NotOpen); credit -= message.Length; continue; }
                    if (peer.BufferedAmount > cap) { Interlocked.Increment(ref stats.Held); break; }
                    if (peer.TrySend(message)) { Interlocked.Increment(ref stats.Sent); added += message.Length; }
                    credit -= message.Length;
                }
                if (now >= nextSample)
                {
                    nextSample = now + 100;
                    var buffered = peer.BufferedAmount;
                    if (buffered <= 0 || lastBuffered + added - buffered > 0) lastProgress = now;
                    stats.MaxStuckMs = Math.Max(stats.MaxStuckMs, now - lastProgress);
                    lastBuffered = buffered;
                    added = 0;
                }
            }
        }
        finally
        {
            stats.Running = false;
            stats.Finished = true;
        }
    }

    private sealed record DownResult(bool Opened, int OpenedAtMs, string[] States, int Received, int Sent, string? Path);

    private async Task<IBrowser> LaunchAsync(string engine)
    {
        try
        {
            return engine == "webkit"
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
    }

    /// <summary>One call: the browser through the shared TURN server, the relay through the transports given.</summary>
    private async Task<(DownResult Browser, RelaySide? Relay)> DownAsync(IBrowser browser, string relay, DownlinkPlan plan, int upKbps = 200,
        int timeoutMs = 20000, string flows = "", string browserTransport = "udp")
    {
        var page = await browser.NewPageAsync();
        try
        {
            await page.GotoAsync(_origin + "/");
            await page.WaitForFunctionAsync("() => window.ready === true");
            var id = Guid.NewGuid().ToString("N");
            var json = await page.EvaluateAsync<string>("options => window.runDown(options).then(result => JSON.stringify(result))", new
            {
                id,
                iceServers = new[] { Turn($"turn:{_turnHost}:3478?transport={browserTransport}", "browser") }
                    .Select(x => new { urls = x.Urls, username = x.Username, credential = x.Credential }),
                relay,
                flows,
                down = plan.Query,
                upKbps,
                seconds = plan.Seconds,
                timeoutMs,
            });
            var result = JsonSerializer.Deserialize<DownResult>(json)!;
            _relays.TryGetValue(id, out var side);
            // The relay's view of its path arrives with its periodic reports.
            for (var wait = 0; result.Opened && side is not null && side.Peer.Path is null && wait < 30; wait++) await Task.Delay(100);
            return (result, side);
        }
        finally { await page.CloseAsync(); }
    }

    private static string Describe(DownResult browser, RelaySide? relay) =>
        $"browser opened={browser.Opened} at {browser.OpenedAtMs} ms, path {browser.Path}, states [{string.Join(", ", browser.States)}], " +
        $"received {browser.Received}; relay path {relay?.Peer.Path?.Describe()}, sent {relay?.Down.Sent}, held {relay?.Down.Held}, " +
        $"not open {relay?.Down.NotOpen}, max stuck {relay?.Down.MaxStuckMs:F0} ms, states [{string.Join(", ", relay?.Down.States ?? [])}], " +
        $"log [{string.Join("; ", relay?.Log ?? [])}]";

    /// <summary>Browser states after the channel first opened, before the run ended: anything but open is a flap.</summary>
    private static string[] BrowserFlaps(DownResult browser) =>
        [.. browser.States.SkipWhile(x => !x.EndsWith(" open")).Skip(1).Where(x => !x.EndsWith(" open") && !x.EndsWith(" closed"))];

    /// <summary>
    /// The production failure, as the workflow models it: half of all UDP flows to the relay's TURN server are dropped,
    /// each flow all or nothing (from xeon-dev's ISP, 67-94% of new UDP flows to Cloudflare's TURN anycast got no answer).
    /// The sidecar starts each allocation from 16 flows, so every call gets a UDP leg, and the TLS fallback is never dialed.
    /// </summary>
    [Category("RelayLeg")]
    [TestCase("chromium")]
    [TestCase("webkit")]
    public async Task WhenTheRelaysNetworkDropsHalfItsUdpFlows_EveryCallStillOpensOverUdp(string engine)
    {
        RequireCondition("half-flows");
        await using var browser = await LaunchAsync(engine);
        var plan = new DownlinkPlan(300, 0, 10, 2);
        for (var call = 0; call < 6; call++)
        {
            var (result, relay) = await DownAsync(browser, "relay-udp,relay-tls", plan);
            TestContext.Out.WriteLine($"{engine} call {call}: {Describe(result, relay)}");
            Assert.Multiple(() =>
            {
                Assert.That(result.Opened, Is.True, Describe(result, relay));
                Assert.That(relay?.Peer.Path?.Describe(), Is.EqualTo("UDP/relay"), "the relay's leg is UDP; TLS is only the fallback");
                Assert.That(result.Received, Is.GreaterThan(0));
            });
        }
    }

    /// <summary>
    /// The same network with one socket per allocation, which is what pion does on its own: a call whose single flow is
    /// dropped gathers nothing on the relay, so its channel never opens. Shows the test models the production failure.
    /// </summary>
    [Category("RelayLeg")]
    [Test]
    public async Task WhenTheRelaysNetworkDropsHalfItsUdpFlows_OneFlowPerAllocation_OftenGathersNothing()
    {
        RequireCondition("half-flows");
        await using var browser = await LaunchAsync("chromium");
        var plan = new DownlinkPlan(100, 0, 10, 1);
        var opened = 0;
        const int calls = 16;
        for (var call = 0; call < calls; call++)
        {
            var (result, relay) = await DownAsync(browser, "relay-udp", plan, timeoutMs: 10000, flows: "1");
            if (result.Opened) opened++;
            TestContext.Out.WriteLine($"one flow, call {call}: opened={result.Opened}; relay log [{string.Join("; ", relay?.Log ?? [])}]");
        }
        TestContext.Out.WriteLine($"one flow per allocation: {opened}/{calls} calls opened");
        Assert.That(calls - opened, Is.GreaterThanOrEqualTo(3), "with half the flows dropped, a single flow fails about every other call");
    }

    /// <summary>
    /// UDP from the relay to its TURN server is blocked outright; its TLS leg is the fallback. The browser's leg stays UDP
    /// (40 ms each way, 1% loss, 4 Mbit/s). The relay sends 2 Mbit/s with a 150 KB keyframe burst every 5 s for 35 s: the
    /// channel must hold, with no ICE disconnect and a buffer that never stops draining for the 2 s stall window. Before
    /// #571 the relay's TLS leg flapped on every keyframe; the phone's own leg then rode TCP too.
    /// </summary>
    [Category("RelayLeg")]
    [TestCase("chromium")]
    [TestCase("webkit")]
    public async Task WhenOnlyTlsReachesTheRelaysTurn_TheCallOpensOverIt_AndCarriesKeyframeBurstsWithoutFlapping(string engine)
    {
        RequireCondition("udp-blocked");
        await HoldsAsync(engine, "TLS/relay", new DownlinkPlan(2000, 150, 5, 35));
    }

    /// <summary>The same run with the relay's UDP leg working: the baseline the TLS fallback is compared with.</summary>
    [Category("RelayLeg")]
    [TestCase("chromium")]
    [TestCase("webkit")]
    public async Task WithTheRelaysUdpLegWorking_TheCallCarriesKeyframeBurstsWithoutFlapping(string engine)
    {
        RequireCondition("clean");
        await HoldsAsync(engine, "UDP/relay", new DownlinkPlan(2000, 150, 5, 35));
    }

    /// <summary>
    /// About half of the relay's UDP round trips to its TURN server are lost (30% of packets each way). The allocation
    /// still succeeds (16 flows race each request), so the leg is UDP, and the call holds: what arrives keeps ICE and
    /// SCTP alive, and the loss is the media's to handle.
    /// </summary>
    [Category("RelayLeg")]
    [TestCase("chromium")]
    [TestCase("webkit")]
    public async Task WhenTheRelaysUdpLosesHalfItsRoundTrips_TheCallStillOpensOverUdp_AndHolds(string engine)
    {
        RequireCondition("lossy");
        await HoldsAsync(engine, "UDP/relay", new DownlinkPlan(600, 40, 5, 25), timeoutMs: 30000, minDelivered: 0.4);
    }

    /// <summary>
    /// Measurement only: the configuration before #571 (the browser's leg over TCP, the relay's over TLS) under the same
    /// browser-leg netem, to see where the TLS-leg flapping came from. Asserts nothing about flaps; logs them.
    /// </summary>
    [Category("RelayLeg")]
    [TestCase("chromium")]
    [TestCase("webkit")]
    public async Task Measure_BothLegsOverTcp_AsBefore571(string engine)
    {
        RequireCondition("udp-blocked");
        await using var browser = await LaunchAsync(engine);
        var (result, relay) = await DownAsync(browser, "relay-udp,relay-tls", new DownlinkPlan(2000, 150, 5, 35), browserTransport: "tcp");
        TestContext.Out.WriteLine($"{engine} before #571 (browser TCP, relay TLS): browser flaps [{string.Join(", ", BrowserFlaps(result))}], " +
                                  $"relay flaps [{string.Join(", ", relay?.Down.Flaps ?? [])}]; {Describe(result, relay)}");
        if (!result.Opened) Assert.Inconclusive("the channel did not open: " + Describe(result, relay));
    }

    private async Task HoldsAsync(string engine, string relayPath, DownlinkPlan plan, int timeoutMs = 20000, double minDelivered = 0.85)
    {
        await using var browser = await LaunchAsync(engine);
        var (result, relay) = await DownAsync(browser, "relay-udp,relay-tls", plan, timeoutMs: timeoutMs);
        var description = Describe(result, relay);
        TestContext.Out.WriteLine($"{engine}: {description}");
        Assert.That(result.Opened, Is.True, description);
        var delivered = relay is { Down.Sent: > 0 } ? (double)result.Received / relay.Down.Sent : 0;
        TestContext.Out.WriteLine($"{engine}: delivered {delivered:P1}, browser flaps {BrowserFlaps(result).Length}, relay flaps {relay?.Down.Flaps.Length}");
        Assert.Multiple(() =>
        {
            Assert.That(relay?.Peer.Path?.Describe(), Is.EqualTo(relayPath));
            Assert.That(result.Path, Is.EqualTo("UDP/relay"), "the browser's own leg is UDP");
            Assert.That(relay?.Down.Flaps, Is.Empty, "the relay's channel never left open while media ran: " + description);
            Assert.That(BrowserFlaps(result), Is.Empty, "the browser's channel never left open while media ran: " + description);
            Assert.That(relay?.Down.MaxStuckMs, Is.LessThan(2000), "the relay's buffer never stopped draining for the stall window");
            Assert.That(delivered, Is.GreaterThanOrEqualTo(minDelivered), "what the relay sent arrived");
        });
    }
}
