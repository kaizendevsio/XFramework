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
        await using var _ = browser;
        var page = await browser.NewPageAsync();
        var console = new List<string>();
        page.Console += (_, message) => { lock (console) if (console.Count < 200) console.Add($"{message.Type}: {message.Text}"); };
        // BENCH_COUNTERS=1: the page marks the start and end of its measurement window, and the iptables counters of the
        // BENCH chain (bytes and packets on each TURN leg, IP headers included) are read at those two moments.
        var wire = new Dictionary<string, Dictionary<string, (long Packets, long Bytes)>>();
        if (Env("BENCH_COUNTERS") == "1")
            await page.ExposeFunctionAsync("benchMark", (string phase) => { lock (wire) wire[phase] = ReadCounters(); return true; });
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
            audioFrameMs = mode == "bolt-after" ? EnvInt("BENCH_AUDIO_FRAME_MS", 20) : 20,
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

    /// <summary>TURN REST credentials (coturn use-auth-secret), UDP only, for one of coturn's two listening addresses.</summary>
    private static object Turn(string host, string secret, string label)
    {
        var username = $"{DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds()}:{label}";
        var credential = Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(username)));
        return new { urls = new[] { $"turn:{host}:3478?transport=udp" }, username, credential };
    }
}
