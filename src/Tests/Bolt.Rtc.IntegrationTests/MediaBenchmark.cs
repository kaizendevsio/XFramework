using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Bolt.Media.Congestion;
using Bolt.Protocol.Transport;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Bolt.Rtc.IntegrationTests;

/// <summary>
/// One run of the media efficiency benchmark (bench/bench.js): the same synthetic camera and microphone at the same
/// target bitrates, through native WebRTC media or Bolt's datagram path, between two peers in one headless Chrome that
/// reach each other only through TURN. The receiver's TURN address (127.0.0.3) is the one the workflow shapes with netem
/// and counts with iptables; the sender's (127.0.0.2) is counted too. Only runs with BENCH_MODE set (see
/// call-media-benchmark.yml); the result is written as JSON to BENCH_OUT.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class MediaBenchmark
{
    private static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;
    private static int EnvInt(string name, int fallback) => int.TryParse(Env(name), out var value) ? value : fallback;

    [Test]
    [CancelAfter(600_000)]
    public async Task Run()
    {
        if (Env("BENCH_MODE") is not { } mode || Env("BOLT_RTC_TURN_SECRET") is not { } secret)
        {
            // The Bolt runs also need BOLT_RTC_SIDECAR: their relay is the production WebRTC endpoint.
            Assert.Ignore("Set BENCH_MODE (native, bolt-before, bolt-after) and BOLT_RTC_TURN_SECRET (see call-media-benchmark.yml).");
            return;
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        await using var app = builder.Build();
        var root = Path.Combine(AppContext.BaseDirectory, "bench");
        app.MapGet("/bench.html", () => Results.Content(
            "<!doctype html><meta charset=\"utf-8\"><title>bench</title><script type=\"module\" src=\"./bench.js\"></script>", "text/html"));
        app.MapGet("/{*path}", (string path) =>
        {
            var file = Path.GetFullPath(Path.Combine(root, path));
            if (!file.StartsWith(root, StringComparison.Ordinal) || !File.Exists(file)) return Results.NotFound();
            var type = Path.GetExtension(file) switch { ".wasm" => "application/wasm", ".js" or ".mjs" => "text/javascript", _ => "application/octet-stream" };
            return Results.File(file, type);
        });
        // The relay for the Bolt runs: the production WebRTC endpoint (the bolt-rtc sidecar through RtcSidecar), relay-only on
        // its own TURN address (127.0.0.4), with the production SCTP window floor; it forwards between the two browsers.
        await using var sidecar = Env("BOLT_RTC_SIDECAR") is { } sidecarPath ? new RtcSidecar(new RtcSidecarOptions { ExecutablePath = sidecarPath }) : null;
        var relays = new System.Collections.Concurrent.ConcurrentDictionary<string, IRtcPeer>();
        app.UseWebSockets();
        app.Map("/relay", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest || sidecar is null) { context.Response.StatusCode = 400; return; }
            var id = context.Request.Query["id"].ToString();
            var partner = id.EndsWith("-sender", StringComparison.Ordinal) ? id[..^7] + "-receiver" : id[..^9] + "-sender";
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var gate = new SemaphoreSlim(1, 1);
            async Task Send(object message)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(message);
                await gate.WaitAsync();
                try { if (socket.State == System.Net.WebSockets.WebSocketState.Open) await socket.SendAsync(bytes, System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None); }
                finally { gate.Release(); }
            }
            var direct = Env("BENCH_DIRECT") == "1";
            var peer = await sidecar.CreateAsync(RtcPeerRole.Answer, new RtcPeerOptions(
                direct ? [] : [ToIceServer(Turn("127.0.0.4", secret, "relay"))], RelayOnly: !direct, RtcDefaults.MaxMessageBytes,
                MinCwndBytes: 128 * 1024, AllowLoopback: direct), CancellationToken.None);
            relays[id] = peer;
            peer.LocalCandidate += candidate => _ = Send(new { type = "candidate", candidate = candidate.Candidate, sdpMid = candidate.SdpMid, sdpMLineIndex = candidate.SdpMLineIndex });
            peer.Message += data => { if (relays.TryGetValue(partner, out var other)) other.TrySend(data.Span); };
            var buffer = new byte[64 * 1024];
            try
            {
                while (socket.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    var received = await socket.ReceiveAsync(buffer, CancellationToken.None);
                    if (received.MessageType == System.Net.WebSockets.WebSocketMessageType.Close) break;
                    using var message = JsonDocument.Parse(buffer.AsMemory(0, received.Count));
                    var root = message.RootElement;
                    switch (root.GetProperty("type").GetString())
                    {
                        case "offer":
                            await Send(new { type = "answer", sdp = await peer.AnswerAsync(root.GetProperty("sdp").GetString()!, CancellationToken.None) });
                            break;
                        case "candidate":
                            await peer.AddCandidateAsync(new RtcCandidate(root.GetProperty("candidate").GetString() ?? "",
                                root.TryGetProperty("sdpMid", out var mid) && mid.ValueKind == JsonValueKind.String ? mid.GetString() : null,
                                root.TryGetProperty("sdpMLineIndex", out var index) && index.ValueKind == JsonValueKind.Number ? index.GetInt32() : null),
                                CancellationToken.None);
                            break;
                    }
                }
            }
            catch (System.Net.WebSockets.WebSocketException) { }
            finally
            {
                relays.TryRemove(id, out _);
                await peer.DisposeAsync();
            }
        });
        await app.StartAsync();
        var origin = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        using var playwright = await Playwright.CreateAsync();
        var launch = new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = ["--disable-features=WebRtcHideLocalIpsWithMdns", "--allow-loopback-in-peer-connection", "--autoplay-policy=no-user-gesture-required"],
        };
        // Google Chrome has the H.264 software encoder and decoder that open-source Chromium builds leave out.
        IBrowser browser;
        try { browser = await playwright.Chromium.LaunchAsync(new(launch) { Channel = Env("BENCH_CHANNEL") ?? "chrome" }); }
        catch (PlaywrightException) { browser = await playwright.Chromium.LaunchAsync(launch); }
        await using var ownedBrowser = browser;
        var page = await browser.NewPageAsync();
        var console = new List<string>();
        page.Console += (_, message) => { lock (console) if (console.Count < 200) console.Add($"{message.Type}: {message.Text}"); };
        // BENCH_COUNTERS=1: the page marks the start and end of its measurement window, and the iptables counters of the
        // BENCH chain (bytes and packets on each TURN leg, IP headers included) are read at those two moments.
        var wire = new Dictionary<string, Dictionary<string, (long Packets, long Bytes)>>();
        if (Env("BENCH_COUNTERS") == "1")
            await page.ExposeFunctionAsync("benchMark", (string phase) => { lock (wire) wire[phase] = ReadCounters(); return true; });
        // The Bolt runs' rate control: the product's SendRateController and AudioPacketization, every 250 ms, fed what the
        // page measured (what it sent, its channel's backlog, the receiver's delay report). Longer Opus packets only
        // "after": the previous client never chose them.
        var videoTarget = EnvInt("BENCH_VIDEO_KBPS", 300);
        var controller = new SendRateController(videoTarget + 32 + 52, new SendRateOptions
        {
            AudioNormalKbps = 32, AudioLowKbps = 24, AudioHighKbps = 32, MaxTotalKbps = videoTarget + 200, RestartFloorKbps = 180 + 84,
        });
        var packets = new AudioPacketization { MaxFrameMs = mode == "bolt-after" ? 60 : 20 };
        if (mode != "native")
            await page.ExposeFunctionAsync("benchRate", (string json) =>
            {
                var sample = JsonDocument.Parse(json).RootElement;
                var now = Environment.TickCount64;
                ReceiverSignal? receiver = sample.GetProperty("receiver").ValueKind == JsonValueKind.Object
                    ? new ReceiverSignal(now, sample.GetProperty("receiver").GetProperty("queueDelayMs").GetInt32(),
                        sample.GetProperty("receiver").GetProperty("receivedKbps").GetInt32())
                    : null;
                SendRateDecision decision;
                int? frameMs;
                lock (controller)
                {
                    decision = controller.Update(new SendPathSample(now, sample.GetProperty("sentKbps").GetInt32(),
                        sample.GetProperty("audioKbps").GetInt32(), sample.GetProperty("localQueueMs").GetInt32(), 0, false, null, receiver));
                    frameMs = packets.Update(controller.CongestionKbps, decision.VideoSuspended, now);
                    if (frameMs is { } applied) packets.Applied(applied);
                }
                return JsonSerializer.Serialize(new
                {
                    totalKbps = decision.TotalKbps, videoKbps = decision.VideoKbps, audioKbps = decision.AudioKbps,
                    frameMs = packets.FrameMs, suspended = decision.VideoSuspended,
                });
            });
        await page.GotoAsync(origin + "/bench.html");
        await page.WaitForFunctionAsync("() => window.benchReady === true", null, new() { Timeout = 30_000 });

        var seconds = EnvInt("BENCH_SECONDS", 60);
        var options = new
        {
            mode,
            seconds,
            warmupSeconds = EnvInt("BENCH_WARMUP_SECONDS", 10),
            width = EnvInt("BENCH_WIDTH", 640),
            height = EnvInt("BENCH_HEIGHT", 360),
            fps = EnvInt("BENCH_FPS", 30),
            videoKbps = EnvInt("BENCH_VIDEO_KBPS", 300),
            audioKbps = EnvInt("BENCH_AUDIO_KBPS", 32),
            // Every run starts at 20 ms; in the Bolt runs the rate control may lengthen packets (bolt-after only).
            audioFrameMs = 20,
            compact = mode == "bolt-after",
            nack = mode == "bolt-after",
            codec = Env("BENCH_CODEC") ?? "h264",
            // BENCH_DIRECT=1: no TURN, the peers meet over loopback directly (a local smoke test of the page, not a measurement).
            turnSender = Env("BENCH_DIRECT") == "1" ? null : Turn("127.0.0.2", secret, "sender"),
            turnReceiver = Env("BENCH_DIRECT") == "1" ? null : Turn("127.0.0.3", secret, "receiver"),
        };
        JsonElement result;
        try
        {
            result = await page.EvaluateAsync<JsonElement>("options => window.runBenchmark(options)", options);
        }
        catch (PlaywrightException error)
        {
            TestContext.Out.WriteLine(string.Join("\n", console));
            Assert.Fail($"The benchmark page failed: {error.Message}");
            return;
        }
        var text = result.GetRawText();
        if (wire.TryGetValue("start", out var start) && wire.TryGetValue("end", out var end))
        {
            var legs = end.ToDictionary(x => x.Key, x => new
            {
                packets = x.Value.Packets - start.GetValueOrDefault(x.Key).Packets,
                bytes = x.Value.Bytes - start.GetValueOrDefault(x.Key).Bytes,
            });
            var merged = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(text)!;
            merged["wire"] = JsonSerializer.SerializeToElement(legs);
            text = JsonSerializer.Serialize(merged);
        }
        TestContext.Out.WriteLine($"BENCH {mode}: {text}");
        if (Env("BENCH_OUT") is { } output) await File.WriteAllTextAsync(output, text);
        if (console.Count > 0) TestContext.Out.WriteLine(string.Join("\n", console.Take(40)));
        Assert.That(result.GetProperty("picture").GetProperty("fps").GetDouble(), Is.GreaterThan(0), "no picture reached the receiver");
    }

    /// <summary>
    /// The BENCH chain's counters by rule comment (sender_up, sender_down, receiver_up, receiver_down): every UDP packet
    /// between each peer and its TURN address, IP and UDP headers included.
    /// </summary>
    private static Dictionary<string, (long Packets, long Bytes)> ReadCounters()
    {
        var counters = new Dictionary<string, (long, long)>();
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("sudo", "-n iptables -L BENCH -n -v -x")
        { RedirectStandardOutput = true, RedirectStandardError = true })!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5_000);
        foreach (var line in output.Split('\n'))
        {
            var comment = System.Text.RegularExpressions.Regex.Match(line, @"/\* (\w+) \*/");
            var columns = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (comment.Success && columns.Length > 2 && long.TryParse(columns[0], out var packets) && long.TryParse(columns[1], out var bytes))
                counters[comment.Groups[1].Value] = (packets, bytes);
        }
        return counters;
    }

    private static RtcIceServer ToIceServer(object turn)
    {
        var element = JsonSerializer.SerializeToElement(turn);
        return new RtcIceServer([.. element.GetProperty("urls").EnumerateArray().Select(x => x.GetString()!)],
            element.GetProperty("username").GetString(), element.GetProperty("credential").GetString());
    }

    /// <summary>TURN REST credentials (coturn use-auth-secret), UDP only, for one of coturn's listening addresses.</summary>
    private static object Turn(string host, string secret, string label)
    {
        var username = $"{DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds()}:{label}";
        var credential = Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(username)));
        return new { urls = new[] { $"turn:{host}:3478?transport=udp" }, username, credential };
    }
}
