using System.Text.Json;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Bolt.Rtc.IntegrationTests;

/// <summary>
/// A phone camera's orientation, end to end in a real browser: a synthetic camera hands the production sender
/// sensor-oriented pixels with rotation and flip as metadata (as Android Chrome does), the real encoder and decoder run,
/// and the receiver's canvas is read back and compared, quadrant by quadrant, with what the browser itself draws for the
/// camera frame. Both ways: orientation sent as metadata (every receiver reads it) and redrawn upright (one does not).
///
/// Runs in installed Chrome (the "chrome" channel; GitHub's Ubuntu runners have it), since VideoFrame orientation is
/// newer than the Chromium this project's Playwright bundles. Needs nothing else: no sidecar, no TURN.
/// </summary>
[TestFixture]
[NonParallelizable]
[CancelAfter(180_000)]
public sealed class VideoOrientationBrowserTests
{
    private const string Origin = "https://bolt-orientation.test";
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        _playwright = await Playwright.CreateAsync();
        try { _browser = await _playwright.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true }); }
        catch (PlaywrightException) when (Environment.GetEnvironmentVariable("CI") is null)
        {
            Assert.Ignore("Google Chrome is not installed; CI runs this test.");
            return;
        }
        _page = await _browser.NewPageAsync();
        var root = AppContext.BaseDirectory;
        // An https origin served from disk: a secure context (WebCodecs needs one) with no server to start.
        await _page.RouteAsync($"{Origin}/**", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath.TrimStart('/');
            if (path is "" or "index.html")
            {
                await route.FulfillAsync(new() { ContentType = "text/html", Body = Page });
                return;
            }
            var file = Path.Combine(root, "orientation", path);
            if (!File.Exists(file)) { await route.FulfillAsync(new() { Status = 404 }); return; }
            await route.FulfillAsync(new() { ContentType = "text/javascript", Body = await File.ReadAllTextAsync(file) });
        });
        await _page.GotoAsync($"{Origin}/");
        await _page.WaitForFunctionAsync("() => window.orientationReady === true");
        if (!await _page.EvaluateAsync<bool>("() => window.orientationCheck.supportsOrientation()"))
        {
            if (Environment.GetEnvironmentVariable("CI") is not null) Assert.Fail("This Chrome predates VideoFrame orientation.");
            Assert.Ignore("This browser predates VideoFrame orientation.");
        }
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    private const string Page = """
        <!doctype html><html><body>
        <script type="module">
            import * as check from './orientation.js';
            window.orientationCheck = check;
            window.orientationReady = true;
        </script>
        </body></html>
        """;

    private async Task<string> CodecAsync()
    {
        foreach (var codec in new[] { "vp9", "h264" })
            if (await _page!.EvaluateAsync<bool>("codec => window.orientationCheck.codecSupported(codec)", codec)) return codec;
        Assert.Fail("No video encoder in this browser.");
        return "";
    }

    private static readonly (int Rotation, bool Flip)[] Orientations =
        [(0, false), (90, false), (180, false), (270, false), (0, true), (90, true), (180, true), (270, true)];

    [TestCase(true)]
    [TestCase(false)]
    public async Task TheReceiverShowsThePictureTheWayTheSendersCameraDoes(bool metadata)
    {
        var codec = await CodecAsync();
        foreach (var (rotation, flip) in Orientations)
        {
            var result = await _page!.EvaluateAsync<JsonElement>("args => window.orientationCheck.roundTrip(args)",
                new { codec, rotation, flip, metadata });
            var label = $"{codec} {rotation}° flip={flip} metadata={metadata}: {result}";
            var sent = result.GetProperty("sent");
            var shown = result.GetProperty("shown");
            var expected = result.GetProperty("expected");
            var code = result.GetProperty("code").GetInt32();
            Assert.Multiple(() =>
            {
                Assert.That(shown.GetProperty("width").GetInt32(), Is.EqualTo(expected.GetProperty("width").GetInt32()), label);
                Assert.That(shown.GetProperty("height").GetInt32(), Is.EqualTo(expected.GetProperty("height").GetInt32()), label);
                var want = Colours(expected.GetProperty("quadrants"));
                var got = Colours(shown.GetProperty("quadrants"));
                for (var q = 0; q < 4; q++)
                    Assert.That(Distance(got[q], want[q]), Is.LessThan(60), $"quadrant {q}: {label}");
                var codes = sent.GetProperty("codes").EnumerateArray().Select(x => x.GetInt32()).ToArray();
                if (metadata)
                {
                    Assert.That(codes, Is.All.EqualTo(code), $"the orientation travels with every picture: {label}");
                    Assert.That((sent.GetProperty("width").GetInt32(), sent.GetProperty("height").GetInt32()), Is.EqualTo((640, 360)),
                        $"the encoder takes the sensor raster: {label}");
                    Assert.That(sent.GetProperty("orientation").GetString(), Is.EqualTo(code == 0 ? "none" : $"sent as metadata ({Angle(code)})"), label);
                    Assert.That(shown.GetProperty("rotation").GetString(), Is.EqualTo(code == 0 ? "none" : Angle(code)), label);
                }
                else
                {
                    Assert.That(codes, Is.All.EqualTo(0), $"upright pixels need no orientation: {label}");
                    Assert.That(sent.GetProperty("width").GetInt32(), Is.EqualTo(expected.GetProperty("width").GetInt32()), label);
                    Assert.That(shown.GetProperty("rotation").GetString(), Is.EqualTo("none"), label);
                }
            });
        }
    }

    [Test]
    public async Task MetadataCostsTheSenderLessThanTheRedraw()
    {
        var codec = await CodecAsync();
        var cost = await _page!.EvaluateAsync<JsonElement>("args => window.orientationCheck.senderCost(args)", new { codec });
        var redraw = cost.GetProperty("redraw");
        var metadata = cost.GetProperty("metadata");
        TestContext.Progress.WriteLine($"Sender cost per rotated 1280x720 frame ({codec}): {cost}");
        Assert.Multiple(() =>
        {
            Assert.That(redraw.GetProperty("how").GetString(), Is.EqualTo("redrawn"));
            Assert.That(metadata.GetProperty("how").GetString(), Is.EqualTo("metadata"));
            Assert.That(metadata.GetProperty("prepareMeanMs").GetDouble(), Is.LessThan(redraw.GetProperty("prepareMeanMs").GetDouble()),
                "taking the orientation off is cheaper than drawing the picture again");
        });
    }

    private static int[][] Colours(JsonElement quadrants) =>
        quadrants.EnumerateArray().Select(x => x.EnumerateArray().Select(c => c.GetInt32()).ToArray()).ToArray();

    private static int Distance(int[] a, int[] b) => a.Zip(b, (x, y) => Math.Abs(x - y)).Max();

    private static string Angle(int code) => $"{(code & 3) * 90}°{((code & 4) != 0 ? ", mirrored" : "")}";
}
