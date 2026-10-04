using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bolt.Protocol.Transport;
using Bolt.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Bolt.Rtc.IntegrationTests;

/// <summary>
/// A whole call between two real browsers, as production runs it: each page is the production browser media stack
/// (Bolt.Media.Browser's BoltMediaService in Blazor WASM, see Bolt.Rtc.CallClient) with only the camera, microphone
/// and decoder replaced by synthetic ones (call/bolt-media.synthetic.js); the relay is the production BoltServer with
/// Yap's call options and its datagram paths through the bolt-rtc sidecar; every leg goes through coturn over UDP.
/// Participant A plays the Android phone of the 2026-10-04 13:08 UTC call (up to 1440p60), B the iPhone (720p30).
///
/// The workflow (call-browser-e2e.yml) shapes each leg with netem and sets CALL_* for the scenario; the test opens the
/// call, lets the rate control climb, measures a window and asserts what a person would see: pictures arriving at the
/// rate they are sent, no freeze, no decoder restarts, audio delivered, and (CALL_CONSTRAINED) a sender that fits its
/// rate to a link that cannot carry it. Every number is printed and written to CALL_OUT.
/// </summary>
[TestFixture]
[NonParallelizable]
[Category("BrowserCall")]
public sealed class BrowserCallTests
{
    private static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;
    private static int EnvInt(string name, int fallback) => int.TryParse(Env(name), out var value) ? value : fallback;

    private sealed record Participant(string Name, string Engine, string Id, string Kid, byte[] Key, string TurnHost, int Height, int Framerate, int Ceiling);

    [Test]
    [CancelAfter(600_000)]
    public async Task ACallKeepsItsPicturesFlowing()
    {
        if (Env("CALL_CLIENT_DIR") is not { } clientDir || Env("BOLT_RTC_SIDECAR") is not { } sidecarPath || Env("BOLT_RTC_TURN_SECRET") is not { } secret)
        {
            Assert.Ignore("Set CALL_CLIENT_DIR (published Bolt.Rtc.CallClient wwwroot), BOLT_RTC_SIDECAR and BOLT_RTC_TURN_SECRET (see call-browser-e2e.yml).");
            return;
        }
        var callId = Guid.NewGuid();
        var a = new Participant("A", Env("CALL_A_ENGINE") ?? "chromium", Media(callId, "a"), "1000001", RandomNumberGenerator.GetBytes(32),
            Env("CALL_A_TURN") ?? "127.0.0.2", EnvInt("CALL_A_HEIGHT", 1440), EnvInt("CALL_A_FPS", 60), EnvInt("CALL_A_CEILING", 1440));
        var b = new Participant("B", Env("CALL_B_ENGINE") ?? "webkit", Media(callId, "b"), "1000002", RandomNumberGenerator.GetBytes(32),
            Env("CALL_B_TURN") ?? "127.0.0.3", EnvInt("CALL_B_HEIGHT", 720), EnvInt("CALL_B_FPS", 30), EnvInt("CALL_B_CEILING", 720));
        var relayTurn = Env("CALL_RELAY_TURN") ?? "127.0.0.4";
        var turnPort = EnvInt("CALL_TURN_PORT", 3478);
        var warmup = EnvInt("CALL_WARMUP_SECONDS", 25);
        var seconds = EnvInt("CALL_SECONDS", 60);
        var direct = Env("CALL_DIRECT") == "1"; // Local smoke run with no TURN (loopback host candidates).

        // ── The relay: Yap's BoltServer options (YapCallGateway), datagram paths through the production sidecar ──
        await using var sidecar = new RtcSidecar(new RtcSidecarOptions { ExecutablePath = sidecarPath }, SidecarLog.Instance);
        using var loggers = LoggerFactory.Create(x => x.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss.fff "; })
            .SetMinimumLevel(LogLevel.Information).AddFilter("Microsoft", LogLevel.Warning));
        var participants = new[] { a, b };
        var ice = new TestIce(secret, relayTurn, turnPort, participants.ToDictionary(x => x.Id, x => x.TurnHost), direct);
        var server = new BoltServer(loggers.CreateLogger<BoltServer>(), new BoltServerOptions
        {
            MediaEnabled = true, RequireSecureTransport = true, AuthenticatedMediaOnly = true, RequireEncryptedMedia = true,
            CallAuthorizer = new Allow(), GroupCallAuthorizer = new Allow(),
            MaxActiveCalls = 64, MaxActiveCallsPerPrincipal = 1, MaxCallParticipants = 8, MaxMediaStreamsPerPrincipal = 2,
            MaxFrameBytes = 64 * 1024, SendQueueCapacity = 64, SendQueueByteCapacity = 1024 * 1024, SendEnqueueTimeoutMs = 250,
            TransportSendStallTimeoutMs = 15_000, MaxConnectionsPerPrincipal = 2, MaxConnectionLifetimeSeconds = 3600,
            MediaTransport = new BoltMediaTransportOptions
            {
                Peers = sidecar, IceServers = ice, RelayOnly = !direct, Logger = loggers.CreateLogger("Bolt.Server.MediaTransport"),
            },
        });
        if (direct) typeof(BoltServer).GetProperty("AllowLoopback", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(server, true);

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        using var certificate = SelfSigned();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
        await using var app = builder.Build();
        app.UseWebSockets();
        var synthetic = Path.Combine(AppContext.BaseDirectory, "call", "bolt-media.synthetic.js");
        var realModule = Path.Combine(clientDir, "_content", "Bolt.Media.Browser", "bolt-media.js");
        // Only bolt-media.js is swapped (for the synthetic devices); the page imports the real one under another name.
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value;
            if (path == "/_content/Bolt.Media.Browser/bolt-media.js") { context.Response.ContentType = "text/javascript"; await context.Response.SendFileAsync(synthetic); return; }
            if (path == "/_content/Bolt.Media.Browser/bolt-media.real.js") { context.Response.ContentType = "text/javascript"; await context.Response.SendFileAsync(realModule); return; }
            await next();
        });
        var types = new FileExtensionContentTypeProvider();
        types.Mappings[".wasm"] = "application/wasm";
        types.Mappings[".mjs"] = "text/javascript";
        types.Mappings[".dat"] = "application/octet-stream";
        types.Mappings[".blat"] = "application/octet-stream";
        types.Mappings[".webcil"] = "application/octet-stream";
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = new PhysicalFileProvider(clientDir) });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(clientDir), ContentTypeProvider = types, ServeUnknownFileTypes = true });
        // The call socket: the identity Yap's ticket would carry, bound to the registration as in production.
        app.Map("/bolt", async (HttpContext context) =>
        {
            if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
            var id = context.Request.Query["id"].ToString();
            if (participants.All(x => x.Id != id)) { context.Response.StatusCode = 403; return; }
            var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("bolt_media_client_id", id), new Claim("sub", id)], "call-test"));
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await server.HandleConnectionAsync(new Bolt.Protocol.Transport.WebSocketBoltConnection(socket), user, context.RequestAborted, isSecureTransport: true);
        });
        // Host admission, as YapCallGateway.Groups does once a participant accepted and connected.
        app.MapPost("/join", async (HttpContext context) =>
        {
            var id = context.Request.Query["id"].ToString();
            for (var attempt = 0; attempt < 50; attempt++)
            {
                if (await server.JoinGroupCallAsync(callId, id)) return Results.Ok();
                await Task.Delay(100);
            }
            return Results.StatusCode(503);
        });
        await app.StartAsync();
        var origin = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        // ── Two browsers ──
        using var playwright = await Playwright.CreateAsync();
        var browsers = new Dictionary<string, IBrowser>();
        async Task<IBrowser> Launch(string engine)
        {
            if (browsers.TryGetValue(engine, out var existing)) return existing;
            var browser = engine == "webkit"
                ? await playwright.Webkit.LaunchAsync(new() { Headless = true })
                : await playwright.Chromium.LaunchAsync(new() { Headless = true, Args = direct ? ["--allow-loopback-in-peer-connection"] : [] });
            return browsers[engine] = browser;
        }
        var pages = new Dictionary<string, IPage>();
        var console = new ConcurrentQueue<string>();
        foreach (var participant in participants)
        {
            var browser = await Launch(participant.Engine);
            var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
            // Each browser reaches the relay only through its own TURN address, so the workflow can shape that leg alone
            // (on loopback a browser would otherwise also pair host and reflexive candidates straight to the relay's).
            if (!direct)
                await context.AddInitScriptAsync("""
                    (() => {
                        const Native = globalThis.RTCPeerConnection;
                        if (!Native) return;
                        const Relayed = function (config, ...rest) {
                            const pc = new Native({ ...(config || {}), iceTransportPolicy: 'relay' }, ...rest);
                            (globalThis.__boltPeers ??= []).push(pc);
                            return pc;
                        };
                        Relayed.prototype = Native.prototype;
                        Object.setPrototypeOf(Relayed, Native);
                        globalThis.RTCPeerConnection = Relayed;
                    })();
                    """);
            var page = await context.NewPageAsync();
            page.Console += (_, message) => { if (console.Count < 400 && message.Type is "error" or "warning") console.Enqueue($"{participant.Name}: {message.Type}: {message.Text}"); };
            page.PageError += (_, error) => console.Enqueue($"{participant.Name}: page error: {error}");
            await page.GotoAsync(origin + "/");
            await page.WaitForFunctionAsync("() => document.getElementById('ready')?.textContent === 'ready'", null, new() { Timeout = 120_000 });
            // CALL_<A|B>_CPU_SLOWDOWN: a phone's CPU, roughly (Chromium only; DevTools CPU throttling).
            if (participant.Engine == "chromium" && EnvInt($"CALL_{participant.Name}_CPU_SLOWDOWN", 1) is > 1 and var slowdown)
            {
                var cdp = await context.NewCDPSessionAsync(page);
                await cdp.SendAsync("Emulation.setCPUThrottlingRate", new Dictionary<string, object> { ["rate"] = slowdown });
            }
            pages[participant.Name] = page;
        }

        string Config(Participant self, Participant other) => JsonSerializer.Serialize(new
        {
            endpoint = origin.Replace("https://", "wss://") + "/bolt?id=" + self.Id,
            join = "/join?id=" + self.Id,
            callId, epoch = "1", binding = Convert.ToHexString(SHA256.HashData(callId.ToByteArray())).ToLowerInvariant(),
            local = new { id = self.Id, kid = self.Kid, key = Convert.ToBase64String(self.Key) },
            remote = new[] { new { id = other.Id, kid = other.Kid, key = Convert.ToBase64String(other.Key) } },
            height = self.Height, framerate = self.Framerate, ceiling = self.Ceiling,
        });
        var started = await Task.WhenAll(
            pages["A"].EvaluateAsync<string>("c => DotNet.invokeMethodAsync('Bolt.Rtc.CallClient', 'Start', c)", Config(a, b)),
            pages["B"].EvaluateAsync<string>("c => DotNet.invokeMethodAsync('Bolt.Rtc.CallClient', 'Start', c)", Config(b, a)));
        Assert.That(started, Is.All.EqualTo("ok"), "both participants joined: " + string.Join(", ", started) + Environment.NewLine + string.Join(Environment.NewLine, console));

        async Task<JsonNode> Stats(string name) => JsonNode.Parse(await pages[name].EvaluateAsync<string>("() => DotNet.invokeMethodAsync('Bolt.Rtc.CallClient', 'Stats')"))!;
        async Task<JsonNode> Result(string name) => JsonNode.Parse(await pages[name].EvaluateAsync<string>("() => JSON.stringify(globalThis.__boltCallResult())"))!;

        var timeline = new List<string>();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        async Task Sample(string label)
        {
            foreach (var name in new[] { "A", "B" })
            {
                var stats = await Stats(name);
                timeline.Add($"{clock.Elapsed.TotalSeconds,6:F1}s {label} {name}: path={stats["path"]} ({stats["pathKind"]}{(stats["pathReason"] is { } r ? " " + r : "")}) " +
                             $"tier={stats["tier"]?.ToJsonString()} rate={stats["rate"]?.ToJsonString()} send={stats["send"]?.ToJsonString()} receive={stats["receive"]?.ToJsonString()}");
            }
            timeline.Add($"{clock.Elapsed.TotalSeconds,6:F1}s {label} relay: {RelayStats(server)}");
            foreach (var name in new[] { "A", "B" })
                timeline.Add($"{clock.Elapsed.TotalSeconds,6:F1}s {label} {name} channel: {await pages[name].EvaluateAsync<string>(ChannelStats)}");
        }

        // Both on their data channels (the production log line "Datagram media path open ... via UDP/relay").
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            if ((await Stats("A"))["pathKind"]?.GetValue<string>() == "Datagram" && (await Stats("B"))["pathKind"]?.GetValue<string>() == "Datagram") break;
            await Task.Delay(500);
        }
        await Sample("open");
        // The rate control climbs from a mobile-safe start (VideoStartTier) on measured headroom, as in production.
        for (var t = 0; t < warmup; t += 5) { await Task.Delay(5_000); await Sample("warmup"); }

        foreach (var page in pages.Values) await page.EvaluateAsync("() => globalThis.__boltCallMeasure()");
        var before = new Dictionary<string, JsonNode> { ["A"] = await Result("A"), ["B"] = await Result("B") };
        for (var t = 0; t < seconds; t += 5) { await Task.Delay(5_000); await Sample("measure"); }
        var after = new Dictionary<string, JsonNode> { ["A"] = await Result("A"), ["B"] = await Result("B") };
        var final = new Dictionary<string, JsonNode> { ["A"] = await Stats("A"), ["B"] = await Stats("B") };

        // ── What each side saw of the other ──
        var summary = new JsonObject { ["scenario"] = Env("CALL_SCENARIO") ?? "local", ["a"] = $"{a.Engine} {a.Height}p{a.Framerate}", ["b"] = $"{b.Engine} {b.Height}p{b.Framerate}" };
        var failures = new List<string>();
        var maxFreezeMs = EnvInt("CALL_MAX_FREEZE_MS", 1000);
        var minFpsRatio = EnvInt("CALL_MIN_FPS_PERCENT", 90) / 100.0;
        foreach (var (sender, receiver) in new[] { ("A", "B"), ("B", "A") })
        {
            var sent = after[sender]["sender"]!;
            var sentFrames = sent["encoded"]!.GetValue<long>() - before[sender]["sender"]!["encoded"]!.GetValue<long>();
            var sentBytes = sent["bytes"]!.GetValue<long>() - before[sender]["sender"]!["bytes"]!.GetValue<long>();
            var windowMs = Math.Max(1, after[receiver]["sender"]!["windowMs"]!.GetValue<long>());
            var sentFps = sentFrames * 1000.0 / windowMs;
            var remote = after[receiver]["remotes"]!.AsArray().FirstOrDefault();
            var beforeRemote = before[receiver]["remotes"]!.AsArray().FirstOrDefault();
            var renderedFps = remote?["fps"]?.GetValue<double>() ?? 0;
            var resets = (remote?["resets"]?.GetValue<int>() ?? 0) - (beforeRemote?["resets"]?.GetValue<int>() ?? 0);
            var corrupt = (remote?["corrupt"]?.GetValue<int>() ?? 0) + (remote?["brokenReference"]?.GetValue<int>() ?? 0);
            var freeze = remote?["longestFreezeMs"]?.GetValue<int>() ?? int.MaxValue;
            var audio = after[receiver]["audio"]!.AsArray().FirstOrDefault();
            var line = new JsonObject
            {
                ["sentFps"] = Math.Round(sentFps, 1), ["sentKbps"] = Math.Round(sentBytes * 8.0 / windowMs), ["renderedFps"] = Math.Round(renderedFps, 1),
                ["longestFreezeMs"] = freeze, ["frozenMs"] = remote?["frozenMs"]?.GetValue<int>(), ["decoderResets"] = resets, ["corrupt"] = corrupt,
                ["largestKeyframe"] = sent["largestKeyframe"]?.GetValue<long>(), ["audioDelivered"] = audio?["delivered"]?.GetValue<double>(),
                ["audioP50"] = audio?["p50"]?.GetValue<int?>(), ["audioP99"] = audio?["p99"]?.GetValue<int?>(),
                ["receive"] = final[receiver]["receive"]?.DeepClone(), ["senderTier"] = final[sender]["tier"]?.DeepClone(), ["senderRate"] = final[sender]["rate"]?.DeepClone(),
                ["senderPath"] = final[sender]["path"]?.DeepClone(), ["receiverPath"] = final[receiver]["path"]?.DeepClone(),
            };
            summary[$"{sender}->{receiver}"] = line;
            if (Env("CALL_CONSTRAINED") is { } constrained && constrained.Split('>') is [var limitedSender, var kbpsText] && limitedSender == sender)
            {
                // The link towards the receiver carries kbpsText: the sender must fit under it, and the picture keep moving.
                var limit = int.Parse(kbpsText);
                var total = final[sender]["rate"]?["totalKbps"]?.GetValue<int>() ?? int.MaxValue;
                if (total > limit) failures.Add($"{sender}->{receiver}: the sender still sends {total} kbps into a {limit} kbps link");
                if (renderedFps <= 0) failures.Add($"{sender}->{receiver}: no picture on the constrained link");
                continue;
            }
            if (renderedFps < sentFps * minFpsRatio) failures.Add($"{sender}->{receiver}: rendered {renderedFps:F1} fps of {sentFps:F1} sent");
            if (freeze > maxFreezeMs) failures.Add($"{sender}->{receiver}: froze for {freeze} ms");
            if (resets > 1) failures.Add($"{sender}->{receiver}: decoder restarted {resets} times");
            if (corrupt > 0) failures.Add($"{sender}->{receiver}: {corrupt} pictures damaged or decoded without their reference");
            if ((audio?["delivered"]?.GetValue<double>() ?? 0) < 0.98) failures.Add($"{sender}->{receiver}: audio delivered {audio?["delivered"]}");
        }
        summary["relay"] = RelayStats(server);
        summary["failures"] = new JsonArray(failures.Select(x => (JsonNode)x).ToArray());
        var text = summary.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        TestContext.Out.WriteLine(text);
        TestContext.Out.WriteLine(string.Join(Environment.NewLine, timeline));
        if (!console.IsEmpty) TestContext.Out.WriteLine(string.Join(Environment.NewLine, console.Take(80)));
        if (Env("CALL_OUT") is { } output) await File.WriteAllTextAsync(output, text);

        foreach (var page in pages.Values) { try { await page.EvaluateAsync("() => DotNet.invokeMethodAsync('Bolt.Rtc.CallClient', 'Stop')"); } catch (PlaywrightException) { } }
        foreach (var browser in browsers.Values) await browser.DisposeAsync();
        await app.StopAsync();
        Assert.That(failures, Is.Empty, text);
    }

    /// <summary>The browser's own view of its data channel: what it sent and received, and what it still holds.</summary>
    private const string ChannelStats = """
        async () => {
            const out = [];
            for (const pc of globalThis.__boltPeers ?? []) {
                if (pc.connectionState === 'closed') continue;
                const report = await pc.getStats();
                for (const s of report.values()) {
                    if (s.type === 'data-channel' && s.label === 'bolt-media')
                        out.push(`msgs sent=${s.messagesSent} recv=${s.messagesReceived} bytes sent=${s.bytesSent} recv=${s.bytesReceived}`);
                    if (s.type === 'candidate-pair' && (s.nominated || s.selected) && s.state === 'succeeded')
                        out.push(`pair rtt=${Math.round((s.currentRoundTripTime ?? 0) * 1000)}ms out=${s.availableOutgoingBitrate ?? '-'} pkts sent=${s.packetsSent ?? '-'} recv=${s.packetsReceived ?? '-'}`);
                }
            }
            return out.join(' | ');
        }
        """;

    /// <summary>A participant's call identity, shaped as Yap's (YapCallGateway.ClientId).</summary>
    private static string Media(Guid call, string who) => $"yap-media-{call:N}-{Guid.NewGuid():N}";

    /// <summary>Per connection: what the relay sent on the datagram path and what its lanes dropped (by reflection; test only).</summary>
    private static string RelayStats(BoltServer server)
    {
        var connections = (System.Collections.IDictionary)typeof(BoltServer).GetField("_connectionsByStreamId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(server)!;
        var parts = new List<string>();
        foreach (var connection in connections.Values)
        {
            var type = connection!.GetType();
            object? Get(string name) => type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(connection);
            var queue = Get("MediaQueue");
            object? Q(string name) => queue?.GetType().GetProperty(name)?.GetValue(queue);
            var datagram = Get("Datagram") as IRtcPeer;
            var id = (Get("ClientId") as string ?? "?");
            parts.Add($"{id[^4..]}: sent={Get("DatagramFramesSent")} socket={Get("DatagramFallbacks")} rexmit={Get("RetransmittedFrames")} " +
                      $"videoDrops={Q("DroppedVideoFrames")} stale={Q("StaleFrames")} purges={Q("VideoPurges")} keyReq={Q("KeyframeRequests")} " +
                      $"layerDrops={Q("LayerDrops")} sidecarDropped={datagram?.Dropped} buffered={datagram?.BufferedAmount} cwnd={datagram?.CongestionWindow}");
        }
        return string.Join(" | ", parts);
    }

    private static X509Certificate2 SelfSigned()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=127.0.0.1", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
        return X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx), null);
    }

    private sealed class Allow : IBoltCallAuthorizer, IBoltGroupCallAuthorizer
    {
        public ValueTask<bool> AuthorizeAsync(BoltCallAuthorizationContext context, CancellationToken ct = default) => ValueTask.FromResult(true);
        public ValueTask<bool> AuthorizeParticipantAsync(Guid callId, string clientId, ClaimsPrincipal participant, CancellationToken ct = default) =>
            ValueTask.FromResult(true);
    }

    /// <summary>TURN REST credentials (coturn use-auth-secret): each browser its own TURN address (its shaped leg), the relay its own.</summary>
    private sealed class TestIce(string secret, string relayHost, int port, IReadOnlyDictionary<string, string> hosts, bool direct) : IBoltIceServerSource
    {
        public ValueTask<BoltIceGrant?> GrantAsync(ClaimsPrincipal participant, CancellationToken ct)
        {
            var id = participant.FindFirst("bolt_media_client_id")?.Value ?? "";
            if (direct)
            {
                // No TURN: a local smoke run; the relay and browsers meet on loopback host candidates.
                var stun = new RtcIceServer([$"stun:127.0.0.1:{port}"]);
                return ValueTask.FromResult<BoltIceGrant?>(new BoltIceGrant([stun], [stun], DateTimeOffset.UtcNow.AddHours(1)));
            }
            return ValueTask.FromResult<BoltIceGrant?>(new BoltIceGrant(
                [Turn(hosts.GetValueOrDefault(id, relayHost), id)], [Turn(relayHost, "relay")], DateTimeOffset.UtcNow.AddHours(1)));
        }

        private RtcIceServer Turn(string host, string label)
        {
            var username = $"{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}:{label[^Math.Min(8, label.Length)..]}";
            var credential = Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(username)));
            return new RtcIceServer([$"turn:{host}:{port}?transport=udp"], username, credential);
        }
    }
}
