using System.Reflection;
using Bolt.Media.Browser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using NUnit.Framework;
using Yap.Client.Services;

namespace Yap.Tests;

// Reading the render tree is exactly the point here: the browser acts on these frames and edits, and
// no public API says whether a node survived a render.
#pragma warning disable BL0006

/// <summary>
/// The call surface is a &lt;dialog&gt; that JS puts in the top layer with showModal(). The top layer
/// belongs to the *node*, not to the markup: a dialog Blazor replaces comes back closed, the chat
/// shows through, and the call carries on unseen behind it. Everything captured inside that dialog -
/// the local preview and the remote tiles - is replaced with it, so a binding taken against the old
/// node paints nothing.
///
/// These render the real component through a real renderer, because a state-flag test cannot tell a
/// surface that is open from one that was opened once.
/// </summary>
public sealed partial class CallSurfaceTests
{
    // Camera on, camera off, camera on: every one of these flips VoiceCall between its two screens.
    [Test]
    public async Task TheCallScreenStaysOpenWhileVideoComesAndGoes()
    {
        await using var call = new SurfaceFixture();
        await call.RenderAsync();
        Assert.Multiple(() =>
        {
            Assert.That(call.Inserted("dialog"), Is.EqualTo(1), "an answered call puts up a dialog");
            Assert.That(call.Opened, Has.Count.EqualTo(1), "and shows it");
        });
        var surface = call.Opened[0];

        foreach (var video in new[] { true, false, true, false })
        {
            await call.SetVideoAsync(video);
            Assert.Multiple(() =>
            {
                Assert.That(call.Inserted("dialog"), Is.EqualTo(1),
                    $"switching to {(video ? "video" : "voice")} must not build a second dialog: a new node is a closed node, "
                    + "and the call would keep running behind the chat");
                Assert.That(call.Label, Is.EqualTo(video ? "Video call" : "Voice call"), "the contents still switch");
                Assert.That(call.Has("canvas"), Is.EqualTo(video), "the video grid is only there on the video screen");
            });
        }

        Assert.That(call.Opened, Is.EqualTo(new[] { surface }),
            "one showModal() is enough precisely because the element never changed");
    }

    // The tiles and the preview do not survive the switch - they live inside the screen that was
    // rebuilt - so the component has to notice and bind the new nodes. Anything that remembers
    // "already attached" per stream rather than per element leaves a black rectangle.
    [Test]
    public async Task ThePreviewAndTilesAreBoundAgainAfterEverySwitch()
    {
        await using var call = new SurfaceFixture();
        await call.RenderAsync();

        for (var pass = 1; pass <= 3; pass++)
        {
            await call.SetVideoAsync(true);
            var preview = call.Element("video");
            var tile = call.Element("canvas");
            Assert.Multiple(() =>
            {
                Assert.That(call.Attached("attachPreview"), Is.EqualTo(preview), $"pass {pass}: the preview on screen is the one showing the camera");
                Assert.That(call.Attached("addRemote"), Is.EqualTo(tile), $"pass {pass}: the canvas on screen is the one being decoded into");
            });
            await call.SetVideoAsync(false);
            Assert.That(call.Element("canvas"), Is.Null, "the tiles are gone with the screen that held them");
        }
    }

    // Minimizing removes the dialog, so expanding is another new node and another open.
    [Test]
    public async Task MinimizingAndExpandingReopensTheSurfaceOnTheNodeThatCameBack()
    {
        await using var call = new SurfaceFixture();
        await call.RenderAsync();
        await call.SetVideoAsync(true);

        await call.SetMinimizedAsync(true);
        Assert.Multiple(() =>
        {
            Assert.That(call.Has("dialog"), Is.False, "minimized shows the pill, not the call screen");
            Assert.That(call.Opened, Has.Count.EqualTo(1));
        });

        await call.SetMinimizedAsync(false);
        Assert.Multiple(() =>
        {
            Assert.That(call.Inserted("dialog"), Is.EqualTo(2), "a fresh dialog element came back");
            Assert.That(call.Opened, Has.Count.EqualTo(2), "and it was opened, because it is not the node we opened before");
            Assert.That(call.Opened[1], Is.Not.EqualTo(call.Opened[0]));
            Assert.That(call.Attached("attachPreview"), Is.EqualTo(call.Element("video")), "the preview is bound to the node that came back");
            Assert.That(call.Attached("addRemote"), Is.EqualTo(call.Element("canvas")));
        });
    }

    // call-screen.js keeps its gesture state on the dialog, so it belongs to the node exactly as the
    // modal state does: once per dialog, untouched by the voice/video switch, and again for the new
    // node that comes back after minimizing.
    [Test]
    public async Task TheGesturesAreMountedOnTheDialogThatIsOpen()
    {
        await using var call = new SurfaceFixture();
        await call.RenderAsync();
        foreach (var video in new[] { true, false, true, false }) await call.SetVideoAsync(video);
        Assert.That(call.Mounted, Is.EqualTo(call.Opened), "one mount, on the one dialog, however often the screen switches");

        await call.SetMinimizedAsync(true);
        await call.SetMinimizedAsync(false);
        Assert.Multiple(() =>
        {
            Assert.That(call.Mounted, Has.Count.EqualTo(2), "the dialog that came back is a new node");
            Assert.That(call.Mounted, Is.EqualTo(call.Opened));
        });
    }

    // The stage is one grid whose base rule was a 2x2 lifted from the prototype. The component has to
    // say how many callers there are, or every layout is that 2x2: one caller in a quarter of it, two
    // callers across the top with a black band underneath.
    [TestCase(0, "one", TestName = "TheGridAsksForTheLayoutThatFitsTheCallers(no remote camera yet)")]
    [TestCase(1, "one")]
    [TestCase(2, "two")]
    [TestCase(3, "many")]
    [TestCase(4, "many")]
    public async Task TheGridAsksForTheLayoutThatFitsTheCallers(int callers, string expected)
    {
        await using var call = new SurfaceFixture();
        await call.RenderAsync();
        await call.SetTilesAsync(callers);

        Assert.That(call.Classes("vidgrid"), Is.EqualTo(new[] { $"vidgrid {expected}" }),
            $"{callers} remote camera(s) must not land in the prototype's 2x2");
        Assert.That(call.Count("canvas"), Is.EqualTo(callers), "one tile per remote camera");
    }

    // Two back chevrons and two names on screen at once looked like the call header rendering twice.
    // It was not: the dialog's background resolved to nothing, so the conversation's own app bar was
    // showing through it. The call screen has one header, and this is what says so.
    [Test]
    public async Task TheCallScreenPutsUpExactlyOneHeader()
    {
        await using var call = new SurfaceFixture();
        await call.RenderAsync();
        foreach (var callers in new[] { 0, 1, 2, 4 })
        {
            await call.SetTilesAsync(callers);
            Assert.Multiple(() =>
            {
                Assert.That(call.Labelled("Back to chat"), Is.EqualTo(1), $"{callers} callers: one way back, not two");
                Assert.That(call.Classes("vidtop"), Has.Length.EqualTo(1), $"{callers} callers: one call header");
            });
        }
    }

    // The self-view is mirrored because a mirror is what people expect of their own face. The back
    // camera is not a mirror: mirroring it reverses any text it is pointed at. Nothing about either
    // changes what is sent, which is never mirrored.
    [TestCase("user", true)]
    [TestCase("environment", false)]
    public async Task OnlyTheFrontCameraSelfViewIsMirrored(string facing, bool mirrored)
    {
        await using var call = new SurfaceFixture();
        await call.RenderAsync();
        await call.SetTilesAsync(1);
        await call.SetFacingAsync(facing);
        Assert.That(call.Classes("pip").Single().Split(' ').Contains("mirror"), Is.EqualTo(mirrored));
    }

    /// <summary>A live call whose camera state the test can move, rendered through a real renderer.</summary>
    private sealed class SurfaceFixture : IAsyncDisposable
    {
        private static readonly Guid Stream = Guid.NewGuid();
        private readonly ServiceProvider provider;
        private readonly HttpClient http = new() { BaseAddress = new("https://yap.test/") };
        private readonly ChatState chat;
        private readonly VoiceState voice;
        private readonly SurfaceRenderer renderer;
        private readonly RecordingJs js = new();
        private readonly object attempt;
        private int component;

        public SurfaceFixture()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IJSRuntime>(js);
            services.AddBoltMediaBrowser();
            provider = services.BuildServiceProvider();
            chat = new ChatState(null!, new ChatApi(http), js);
            voice = new VoiceState(chat, new ChatApi(http), provider.GetRequiredService<IServiceScopeFactory>(),
                new SurfaceNavigation(), NullLoggerFactory.Instance, js);

            // A connected one-to-one call. Nothing here can be reached from outside VoiceState, and
            // nothing short of a real camera and a real peer would produce it.
            var media = provider.GetRequiredService<BoltMediaService>();
            ((Dictionary<Guid, RemoteVideoStream>)Field(typeof(BoltMediaService), "_remoteVideo").GetValue(media)!)[Stream]
                = new(Stream, "peer", VideoCodec.H264);
            var type = typeof(VoiceState).GetNestedType("Attempt", BindingFlags.NonPublic)!;
            attempt = Activator.CreateInstance(type,
                BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ["account"], null)!;
            type.GetField("Media")!.SetValue(attempt, media);
            Field(typeof(VoiceState), "active").SetValue(voice, attempt);
            typeof(VoiceState).GetProperty(nameof(VoiceState.Status))!.SetValue(voice, "Connected");

            var container = new ServiceCollection();
            container.AddSingleton<IJSRuntime>(js);
            container.AddSingleton(voice);
            renderer = new SurfaceRenderer(container.BuildServiceProvider());
        }

        public Task RenderAsync() => renderer.Dispatcher.InvokeAsync(async () =>
        {
            component = renderer.Attach(typeof(Yap.Client.Components.VoiceCall));
            await renderer.RenderAsync(component);
        });

        public Task SetVideoAsync(bool on) => ChangeAsync(() =>
        {
            var type = attempt.GetType();
            type.GetField("CameraOn")!.SetValue(attempt, on);
            type.GetField("Tiles")!.SetValue(attempt, on ? (IReadOnlyList<VideoTile>)[new VideoTile(Stream, Guid.NewGuid())] : []);
        });

        public Task SetMinimizedAsync(bool minimized) => ChangeAsync(() => voice.Minimized = minimized);

        public Task SetFacingAsync(string facing) => ChangeAsync(() => attempt.GetType().GetField("Facing")!.SetValue(attempt, facing));

        /// <summary>Put <paramref name="count"/> remote cameras on the call, each its own stream.</summary>
        public Task SetTilesAsync(int count) => ChangeAsync(() =>
        {
            var media = provider.GetRequiredService<BoltMediaService>();
            var streams = (Dictionary<Guid, RemoteVideoStream>)Field(typeof(BoltMediaService), "_remoteVideo").GetValue(media)!;
            var tiles = new List<VideoTile>();
            for (var i = 0; i < count; i++)
            {
                // The first is the stream the fixture already registered, so the existing tests keep
                // the element identity they assert on.
                var id = i == 0 ? Stream : Guid.NewGuid();
                streams[id] = new(id, $"peer{i}", VideoCodec.H264);
                tiles.Add(new(id, Guid.NewGuid()));
            }
            var type = attempt.GetType();
            // Camera on regardless: the video screen is what has a grid, and a call with remote
            // cameras and none of its own must still show it.
            type.GetField("CameraOn")!.SetValue(attempt, true);
            type.GetField("Tiles")!.SetValue(attempt, (IReadOnlyList<VideoTile>)tiles);
        });

        public VoiceState Voice => voice;
        public object CurrentAttempt => attempt;

        public Task ChangeAsync(Action change) => renderer.Dispatcher.InvokeAsync(() =>
        {
            change();
            typeof(VoiceState).GetMethod("Notify", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(voice, null);
        // The component answers Changed with InvokeAsync, so a second hop through the dispatcher is
        // what "the render and its OnAfterRender have finished" means here.
        }).ContinueWith(_ => renderer.Dispatcher.InvokeAsync(() => { })).Unwrap();

        /// <summary>
        /// How many times Blazor has told the browser to create a <paramref name="element"/> node. This is
        /// the signal the bug turned on: a second dialog is a second node, and only the first was opened.
        /// </summary>
        public int Inserted(string element) => renderer.Inserted.Count(x => x == element);
        public string? Label => Frames().Where(x => x.FrameType == RenderTreeFrameType.Attribute && x.AttributeName == "aria-label")
            .Select(x => x.AttributeValue as string).FirstOrDefault(x => x is "Voice call" or "Video call");
        public bool Has(string element) => Frames().Any(x => x.FrameType == RenderTreeFrameType.Element && x.ElementName == element);
        public int Count(string element) => Frames().Count(x => x.FrameType == RenderTreeFrameType.Element && x.ElementName == element);

        /// <summary>Every rendered class attribute that names <paramref name="css"/>, in tree order.</summary>
        public string[] Classes(string css) => Frames()
            .Where(x => x.FrameType == RenderTreeFrameType.Attribute && x.AttributeName == "class")
            .Select(x => (x.AttributeValue as string)?.Trim() ?? "")
            .Where(value => value.Split(' ').Contains(css))
            .ToArray();

        public int Labelled(string label) => Frames().Count(x => x.FrameType == RenderTreeFrameType.Attribute
            && x.AttributeName == "aria-label" && (x.AttributeValue as string) == label);

        /// <summary>The reference id Blazor gave the first <paramref name="element"/> it has just created.</summary>
        public string? Element(string element)
        {
            var frames = Frames().ToArray();
            for (var i = 0; i < frames.Length; i++)
            {
                if (frames[i].FrameType != RenderTreeFrameType.Element || frames[i].ElementName != element) continue;
                // The capture sits with the element's own attributes, before anything nested inside it.
                for (var j = i + 1; j < frames.Length; j++)
                {
                    if (frames[j].FrameType == RenderTreeFrameType.ElementReferenceCapture) return frames[j].ElementReferenceCaptureId;
                    if (frames[j].FrameType != RenderTreeFrameType.Attribute) break;
                }
            }
            return null;
        }

        /// <summary>The element ids yap.openCall was asked to show, in order.</summary>
        public IReadOnlyList<string> Opened => js.Calls.Where(x => x.Name == "yap.openCall").Select(x => Reference(x.Args)).ToArray();

        /// <summary>The element ids call-screen.js was mounted on, in order.</summary>
        public IReadOnlyList<string> Mounted => js.Calls.Where(x => x.Name == "mount").Select(x => Reference(x.Args)).ToArray();

        /// <summary>The element the media pipeline was last pointed at by <paramref name="call"/>.</summary>
        public string? Attached(string call) => js.Calls.Where(x => x.Name == call).Select(x => Reference(x.Args)).LastOrDefault();

        private static string Reference(object?[] args) => args.OfType<ElementReference>().Select(x => x.Id).FirstOrDefault() ?? "";
        private IEnumerable<RenderTreeFrame> Frames() => renderer.Tree(component);
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!;

        public async ValueTask DisposeAsync()
        {
            // The fabricated attempt was never started, so let VoiceState tear down an idle state.
            Field(typeof(VoiceState), "active").SetValue(voice, null);
            renderer.Dispose();
            await voice.DisposeAsync();
            await chat.DisposeAsync();
            await provider.DisposeAsync();
            http.Dispose();
        }
    }

    private sealed class SurfaceRenderer(IServiceProvider services) : Renderer(services, NullLoggerFactory.Instance)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        protected override void HandleException(Exception exception) => throw exception;

        /// <summary>Element names this batch told the browser to insert - i.e. to create as new nodes.</summary>
        public List<string> Inserted { get; } = [];

        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
        {
            for (var component = 0; component < renderBatch.UpdatedComponents.Count; component++)
            {
                var edits = renderBatch.UpdatedComponents.Array[component].Edits;
                for (var i = 0; i < edits.Count; i++)
                {
                    var edit = edits.Array[edits.Offset + i];
                    if (edit.Type != RenderTreeEditType.PrependFrame) continue;
                    var frame = renderBatch.ReferenceFrames.Array[edit.ReferenceFrameIndex];
                    if (frame.FrameType == RenderTreeFrameType.Element) Inserted.Add(frame.ElementName);
                }
            }
            return Task.CompletedTask;
        }

        // InstantiateComponent, not new: it is what performs the component's [Inject] wiring.
        public int Attach(Type component) => AssignRootComponentId(InstantiateComponent(component));
        public Task RenderAsync(int id) => RenderRootComponentAsync(id);
        public IEnumerable<RenderTreeFrame> Tree(int id)
        {
            var frames = GetCurrentRenderTreeFrames(id);
            var all = new List<RenderTreeFrame>();
            Walk(frames, all);
            return all;
        }

        // A component's own frames stop at its children, so the tree is gathered component by component.
        private void Walk(ArrayRange<RenderTreeFrame> frames, List<RenderTreeFrame> into)
        {
            for (var i = 0; i < frames.Count; i++)
            {
                var frame = frames.Array[i];
                into.Add(frame);
                if (frame.FrameType == RenderTreeFrameType.Component && frame.ComponentId != 0)
                    Walk(GetCurrentRenderTreeFrames(frame.ComponentId), into);
            }
        }
    }

    /// <summary>Records every interop call, and hands back a recorder for any module it is asked to import.</summary>
    private sealed class RecordingJs : IJSRuntime, IJSObjectReference
    {
        public List<(string Name, object?[] Args)> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            lock (Calls) Calls.Add((identifier, args ?? []));
            object? value = typeof(TValue) == typeof(IJSObjectReference) ? this
                : typeof(TValue) == typeof(bool) ? true : default(TValue);
            return ValueTask.FromResult((TValue)value!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SurfaceNavigation : NavigationManager
    { public SurfaceNavigation() => Initialize("https://yap.test/", "https://yap.test/"); }
}
#pragma warning restore BL0006
