using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Components;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Tests;

/// <summary>
/// The call screen's markup contract, rendered from the real component in each phase of a call. The
/// gestures in call-screen.js find their targets by these hooks, so a renamed class is a dead gesture
/// rather than a compile error; and every control must say what it does, because on the video screen
/// the buttons are icons only.
///
/// With YAP_UI_ARTIFACT_DIR set, each phase is also written out as a standalone page carrying the real
/// stylesheets and the built scoped bundle, for screenshotting at phone size.
/// </summary>
public sealed class CallScreenTests
{
    private static readonly Guid Self = Guid.NewGuid(), Alex = Guid.NewGuid(), Jamie = Guid.NewGuid(), Sam = Guid.NewGuid();

    [Test]
    public async Task EveryControlOnEveryScreenHasAName()
    {
        foreach (var (name, setup) in Phases())
        {
            var html = await RenderAsync(name, setup);
            foreach (System.Text.RegularExpressions.Match button in Regex.Matches(html, "<button[^>]*>"))
                Assert.That(button.Value, Does.Match("aria-label=\"[^\"]+\""), $"{name}: {button.Value} has no accessible name");
        }
    }

    [Test]
    public async Task OnlyTheVideoHeaderAndTrayAreHiddenByATap()
    {
        var video = await RenderAsync("video-1to1", Video(1));
        Assert.Multiple(() =>
        {
            Assert.That(Regex.Matches(video, "data-chrome-part").Count, Is.EqualTo(2), "the header and the control tray, nothing else");
            Assert.That(video, Does.Match("<header class=\"vidtop\"[^>]*data-chrome-part"));
            Assert.That(video, Does.Match("class=\"call-tray\"[^>]*data-chrome-part"));
        });
        // Voice has nothing to look at behind the controls, so it has nothing to hide them for.
        var voice = await RenderAsync("voice-active", Voice(connected: true));
        Assert.That(voice, Does.Not.Contain("data-chrome-part"));
    }

    // The options panel must stop the controls hiding while it is open; call-screen.js looks for this.
    [Test]
    public async Task TheOptionsButtonSaysWhetherItsPanelIsOpen()
    {
        var html = await RenderAsync("video-1to1", Video(1));
        Assert.That(html, Does.Contain("aria-label=\"Audio and video options\" aria-expanded=\"false\""));
    }

    [Test]
    public async Task AGroupShowsCameraOffPeopleAsAvatarTilesWithTheirMuteState()
    {
        var html = await RenderAsync("group-camera-off", Group(videoFor: [Alex], muted: [Jamie]));
        Assert.Multiple(() =>
        {
            Assert.That(Regex.Matches(html, "<canvas").Count, Is.EqualTo(1), "one live camera");
            Assert.That(Regex.Matches(html, "class=\"vidtile camera-off\"").Count, Is.EqualTo(2), "two people with their cameras off, as people");
            Assert.That(html, Does.Contain("class=\"vidgrid many\""), "three remote people is the multi-tile layout");
            Assert.That(Regex.Matches(html, "class=\"tile-name\"").Count, Is.EqualTo(3), "every tile is named in a group");
            Assert.That(html, Does.Not.Contain(">You<"), "you are the self-view, never a tile");
        });
    }

    [Test]
    public async Task OneToOneVideoIsNotLabelledOverTheirFace()
    {
        var html = await RenderAsync("video-1to1", Video(1));
        Assert.That(html, Does.Not.Contain("class=\"tile-name\""), "the header already says who this is");
    }

    [TestCase(false, new[] { "Decline", "Accept" })]
    [TestCase(true, new[] { "Decline", "Audio only", "Accept video" })]
    public async Task AnIncomingCallOffersLabelledAnswers(bool video, string[] answers)
    {
        var html = await RenderAsync("incoming", Incoming(video));
        var labels = Regex.Matches(html, "<span class=\"lbl\"[^>]*>([^<]+)</span>").Select(x => x.Groups[1].Value).ToArray();
        Assert.That(labels, Is.EqualTo(answers));
    }

    [Test]
    public async Task RingingWithTheCameraOnPutsYourselfOnTheStage()
    {
        var html = await RenderAsync("video-ringing", state =>
        {
            Voice(connected: false)(state);
            state.Attempt("CameraOn", true);
        });
        Assert.That(html, Does.Contain("self-stage"));
        Assert.That(html, Does.Contain("class=\"stage-caption\""));
    }

    [Test]
    public async Task TheCallTimerKeepsCountingPastAnHour()
    {
        var html = await RenderAsync("voice-long", state =>
        {
            Voice(connected: true)(state);
            state.Set(nameof(VoiceState.ConnectedAt), (DateTimeOffset?)DateTimeOffset.UtcNow.AddMinutes(-75));
        });
        Assert.That(html, Does.Contain(">1:15:0"), "mm:ss wrapped back to 15:00 after an hour");
    }

    // ---------------------------------------------------------------------------------------------

    /// <summary>The phases worth looking at, as the artifacts and the naming test both need them.</summary>
    private static IEnumerable<(string Name, Action<CallState> Setup)> Phases() =>
    [
        ("voice-outgoing", Voice(connected: false)),
        ("voice-outgoing-photo", state => { Voice(connected: false)(state); state.Set(nameof(VoiceState.AvatarUrl), Photo); }),
        ("incoming", Incoming(false)),
        ("incoming-video", Incoming(true)),
        ("voice-active", Voice(connected: true)),
        ("video-1to1", Video(1)),
        ("video-1to1-hidden", Video(1)),
        ("video-1to1-swapped", Video(1)),
        ("video-camera-off", Group(videoFor: [], muted: [], people: [Alex])),
        ("video-grid-3", Group(videoFor: [Alex, Jamie, Sam], muted: [Jamie])),
        ("group-camera-off", Group(videoFor: [Alex], muted: [Jamie])),
        ("video-options", Video(1)),
        ("voice-options", Voice(connected: true)),
        ("minimized", state => { Video(1)(state); state.Set(nameof(VoiceState.Minimized), true); }),
    ];

    private const string Photo = "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'%3E%3Cdefs%3E%3ClinearGradient id='g' x1='0' y1='0' x2='1' y2='1'%3E%3Cstop offset='0' stop-color='%23f0a35e'/%3E%3Cstop offset='1' stop-color='%23b8336a'/%3E%3C/linearGradient%3E%3C/defs%3E%3Crect width='100' height='100' fill='url(%23g)'/%3E%3Ccircle cx='50' cy='40' r='18' fill='%23ffd9b8'/%3E%3Cpath d='M18 100c4-24 18-34 32-34s28 10 32 34' fill='%23263a6b'/%3E%3C/svg%3E";

    private static Action<CallState> Voice(bool connected) => state =>
    {
        state.Set(nameof(VoiceState.Name), "Bunsoy");
        state.Set(nameof(VoiceState.Status), connected ? "Connected" : "Ringing...");
        if (connected) state.Set(nameof(VoiceState.ConnectedAt), (DateTimeOffset?)DateTimeOffset.UtcNow.AddSeconds(-83));
        state.Set(nameof(VoiceState.VideoAvailable), true);
    };

    private static Action<CallState> Incoming(bool video) => state =>
    {
        state.Set(nameof(VoiceState.Name), "Bunsoy");
        state.Set(nameof(VoiceState.Status), "Incoming encrypted voice call");
        state.Set(nameof(VoiceState.Incoming), true);
        state.Set(nameof(VoiceState.VideoAvailable), true);
        state.Attempt("Group", new YapGroupCall(Guid.NewGuid(), Guid.NewGuid(), Alex, "Bunsoy", 1, DateTimeOffset.UtcNow.AddMinutes(1),
            [new(Alex, Guid.NewGuid(), true, true, false, false, video), new(Self, Guid.NewGuid(), false, false, false, false)], video));
    };

    private static Action<CallState> Video(int remote) => state =>
    {
        Voice(connected: true)(state);
        state.Attempt("CameraOn", true);
        state.Attempt("Tiles", (IReadOnlyList<VideoTile>)Enumerable.Range(0, remote).Select(_ => new VideoTile(Guid.NewGuid(), Alex)).ToArray());
        state.Attempt("Group", new YapGroupCall(Guid.NewGuid(), Guid.NewGuid(), Alex, "Bunsoy", 1, DateTimeOffset.UtcNow.AddMinutes(1),
            [new(Alex, Guid.NewGuid(), true, true, false, false, true), new(Self, Guid.NewGuid(), true, true, false, false, true)]));
    };

    private static Action<CallState> Group(Guid[] videoFor, Guid[] muted, Guid[]? people = null) => state =>
    {
        Voice(connected: true)(state);
        people ??= [Alex, Jamie, Sam];
        state.Set(nameof(VoiceState.Name), people.Length > 1 ? "Weekend plans" : "Alex Rivera");
        state.Attempt("CameraOn", true);
        state.Attempt("Tiles", (IReadOnlyList<VideoTile>)videoFor.Select(id => new VideoTile(Guid.NewGuid(), id)).ToArray());
        state.Attempt("Group", new YapGroupCall(Guid.NewGuid(), Guid.NewGuid(), Alex, "Alex", 1, DateTimeOffset.UtcNow.AddMinutes(1),
            [.. people.Select(id => new YapGroupParticipant(id, Guid.NewGuid(), true, true, false, muted.Contains(id), videoFor.Contains(id))),
             new(Self, Guid.NewGuid(), true, true, false, false, true)]));
    };

    private static async Task<string> RenderAsync(string name, Action<CallState> setup)
    {
        await using var state = new CallState();
        setup(state);
        var html = await state.RenderAsync(name.EndsWith("-options"));
        await SaveArtifactAsync(name, html);
        return html;
    }

    /// <summary>A VoiceState with a live attempt whose fields the test can set, as CallSurfaceTests does.</summary>
    private sealed class CallState : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly HttpClient http = new() { BaseAddress = new("https://yap.test/") };
        private readonly ChatState chat;
        private readonly VoiceState voice;
        private readonly object attempt;

        public CallState()
        {
            var js = new Mock<IJSRuntime>().Object;
            chat = new ChatState(null!, new ChatApi(http), js);
            typeof(ChatState).GetProperty(nameof(ChatState.User))!.SetValue(chat, new UserSession(Self, Guid.NewGuid(), "Me"));
            typeof(ChatState).GetProperty(nameof(ChatState.Selected))!.SetValue(chat, new Conversation
            {
                Id = Guid.NewGuid(), Name = "Weekend plans",
                People = [new(Self, "Me", "me"), new(Alex, "Alex Rivera", "alex"), new(Jamie, "Jamie Cruz", "jamie"), new(Sam, "Sam Lee", "sam")],
            });
            var scopes = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
            voice = new VoiceState(chat, new ChatApi(http), scopes, new Navigation(), NullLoggerFactory.Instance, js);
            var type = typeof(VoiceState).GetNestedType("Attempt", BindingFlags.NonPublic)!;
            attempt = Activator.CreateInstance(type,
                BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["account"], null)!;
            typeof(VoiceState).GetField("active", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(voice, attempt);

            var services = new ServiceCollection().AddLogging();
            services.AddSingleton(js);
            services.AddSingleton(voice);
            provider = services.BuildServiceProvider();
        }

        public void Set(string property, object? value) => typeof(VoiceState).GetProperty(property)!.SetValue(voice, value);
        public void Attempt(string field, object? value) => attempt.GetType().GetField(field)!.SetValue(attempt, value);

        public async Task<string> RenderAsync(bool options)
        {
            await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
            return await renderer.Dispatcher.InvokeAsync(async () => options
                ? (await renderer.RenderComponentAsync<OptionsOpen>()).ToHtmlString()
                : (await renderer.RenderComponentAsync<VoiceCall>()).ToHtmlString());
        }

        public async ValueTask DisposeAsync()
        {
            typeof(VoiceState).GetField("active", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(voice, null);
            await voice.DisposeAsync();
            await chat.DisposeAsync();
            await provider.DisposeAsync();
            http.Dispose();
        }
    }

    /// <summary>The call screen with its options panel already open, which only a tap can otherwise do.</summary>
    private sealed class OptionsOpen : VoiceCall
    {
        protected override void OnInitialized()
        {
            base.OnInitialized();
            typeof(VoiceCall).GetField("outputOpen", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(this, true);
        }
    }

    private sealed class Navigation : NavigationManager
    { public Navigation() => Initialize("https://yap.test/", "https://yap.test/"); }

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A standalone page per phase: the stylesheets in index.html's order, the built scoped bundle, the
    /// real call-screen.js mounted on the dialog, and painted stand-ins for camera pictures.
    /// </summary>
    private static async Task SaveArtifactAsync(string name, string html)
    {
        if (Environment.GetEnvironmentVariable("YAP_UI_ARTIFACT_DIR") is not { Length: > 0 } directory) return;
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "CLAUDE.md"))) root = root.Parent;
        var client = Path.Combine(root!.FullName, "src/Presentation/XFramework.Yap.Client");
        var bundle = Directory.EnumerateFiles(Path.Combine(client, "obj"), "XFramework.Yap.Client.styles.css", SearchOption.AllDirectories)
            .Where(x => x.Contains("scopedcss" + Path.DirectorySeparatorChar + "bundle")).OrderByDescending(File.GetLastWriteTimeUtc).First();
        string Sheet(string file) => File.ReadAllText(Path.Combine(client, "wwwroot", file));
        var css = string.Join("\n", new[] { "app.css", "brand.css", "motion.css", "glass.css" }.Select(Sheet))
            + "\n" + File.ReadAllText(bundle) + "\n" + Sheet("mobile.css") + "\n" + Sheet("details.css");
        var script = Sheet("call-screen.js");
        var dialog = html.Replace("<dialog class=\"voice-dialog\"", "<dialog open class=\"voice-dialog\"");
        var attributes = name.EndsWith("-hidden") ? "data-chrome=\"hidden\"" : name.EndsWith("-swapped") ? "data-swapped" : "";
        if (attributes.Length > 0) dialog = dialog.Replace("<dialog open", "<dialog open " + attributes);
        Directory.CreateDirectory(directory);
        foreach (var theme in new[] { "light", "dark" })
            await File.WriteAllTextAsync(Path.Combine(directory, $"{name}-{theme}.html"), $$"""
                <!doctype html><html data-theme="{{theme}}"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
                <style>{{css}}
                /* A notched phone: desktop Chrome reports no safe areas. */
                html .voice-dialog[open]{--call-top:54px!important;--call-bottom:34px!important}
                *,*::before,*::after{animation-duration:0s!important;transition-duration:0s!important}
                /* Headless Chrome will not open a window narrower than ~500px, so the phone is a
                   transformed box: that makes it the containing block for the fixed dialog. */
                body{margin:0}.phone-frame{position:relative;width:375px;height:812px;overflow:hidden;transform:translateZ(0)}</style>
                <body><div class="phone-frame">{{dialog}}</div>
                <script type="module">
                {{script}}
                const paint = (w, h, hue, seed) => {
                    const c = document.createElement('canvas'); c.width = w; c.height = h; const g = c.getContext('2d');
                    const bg = g.createLinearGradient(0, 0, w, h);
                    bg.addColorStop(0, `hsl(${hue} 32% 62%)`); bg.addColorStop(1, `hsl(${hue + 40} 30% 26%)`);
                    g.fillStyle = bg; g.fillRect(0, 0, w, h);
                    g.fillStyle = 'rgba(255,255,255,.18)'; g.fillRect(w * .08, h * .1, w * .28, h * .5);
                    const s = Math.min(w, h);
                    g.fillStyle = `hsl(${25 + seed * 8} 45% 70%)`; g.beginPath(); g.ellipse(w / 2, h * .42, s * .2, s * .25, 0, 0, 7); g.fill();
                    g.fillStyle = `hsl(${hue + 180} 35% 30%)`; g.beginPath(); g.ellipse(w / 2, h * 1.02, s * .52, s * .42, 0, 0, 7); g.fill();
                    return c;
                };
                document.querySelectorAll('.vidtile canvas').forEach((canvas, i) => {
                    const src = paint(720, 1280, 200 + i * 55, i); canvas.width = src.width; canvas.height = src.height;
                    canvas.getContext('2d').drawImage(src, 0, 0);
                });
                document.querySelectorAll('.pip video').forEach(video => { video.poster = paint(720, 1280, 20, 3).toDataURL(); });
                const root = document.querySelector('dialog');
                if (root) mount(root);
                </script></body></html>
                """);
    }
}
