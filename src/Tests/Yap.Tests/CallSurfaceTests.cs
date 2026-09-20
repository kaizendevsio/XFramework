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
public sealed class CallSurfaceTests
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

        private Task ChangeAsync(Action change) => renderer.Dispatcher.InvokeAsync(() =>
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
