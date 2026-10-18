using System.Buffers;
using System.Collections.Concurrent;
using System.Reflection;
using Bolt.Client;
using Bolt.Media;
using Bolt.Media.Browser;
using Bolt.Media.Congestion;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// The browser media service across a transport swap: a hosted SFrame call survives its socket,
/// keeps its keys and capture, and republishes fresh streams on the next connection.
/// </summary>
public sealed class BoltMediaServiceResumeTests
{
    [TestCase("relay")]
    [TestCase("receiver")]
    [TestCase("transport")]
    public async Task PendingProbe_StillCutsThePreviousWebSocketRateFromMediaFeedback(string source)
    {
        await using var f = await Fixture.CreateAsync();
        await f.Media.StartHostedAudioAsync(f.Call);
        await f.Media.StartVideoAsync(f.Call, VideoCodec.H264, 1080);
        await PauseRateAsync(f.Media);
        var loop = RateLoop(f.Media)!;
        loop.Controller.Reset(8_496);
        loop.Pacer.RateKbps = 8_496;
        loop.Ladder.Restart(8_496 - 84);
        await (Task)typeof(BoltMediaService).GetMethod("ApplyVideoAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(f.Media, [new SendRateTick(default, loop.Ladder.Current, false, false, null, default)])!;
        Assert.That(f.Js.AppliedVideoTiers[^1].Height, Is.EqualTo(1080));
        Assert.That(f.Js.AppliedVideoTiers[^1].BitrateKbps, Is.EqualTo(5600));
        var transport = PendingProbe(f.Media);
        var stream = f.FirstTransport.Configs(MediaType.Video).Single();
        using var running = new CancellationTokenSource();
        var task = RunRateAsync(f.Media, loop, running.Token);
        try
        {
            await EncodeAudioAsync(f.Media);
            Assert.That(() => f.FirstTransport.MediaFrames(), Is.GreaterThan(0).After(1000, 10), "media continues while the probe waits");
            var frame = new ArrayBufferWriter<byte>();
            if (source == "relay")
                BoltCodec.WriteMediaCongestion(frame, new MediaCongestionData
                {
                    StreamId = stream, QueueDelayMs = 1_200, AllowedKbps = 512,
                    Flags = MediaCongestionFlags.Limited | MediaCongestionFlags.BaseLayerLost,
                });
            else if (source == "receiver")
                BoltCodec.WriteMediaFeedback(frame, stream, 1, 0, 0, 0, QualityHint.Maintain, 1_200, 512);
            else
                loop.Signals.OnTransportFeedback(new(Environment.TickCount64, 1_200, 0, 0.2, 512));
            if (frame.WrittenCount > 0) Assert.That(f.First.DispatchDatagram(frame.WrittenSpan), Is.EqualTo(1));
            Assert.That(() => loop.Pacer.RateKbps, Is.LessThan(8_496).After(1200, 20),
                "the old path's rate must respond to media congestion before the probe finishes");
            Assert.That(() => f.Js.AppliedVideoTiers[^1].BitrateKbps, Is.LessThan(5600).After(1000, 20),
                "the encoder follows the cut while the probe is still pending");
            Assert.That(transport.Probing, Is.True);
        }
        finally
        {
            await running.CancelAsync();
            await task;
            typeof(MediaTransportClient).GetField("_probe", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(transport, null);
        }
    }

    [Test]
    public async Task PendingProbe_IgnoresPaddingLocalQueue_ButKeepsAudioAdaptationRunning()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Media.StartHostedAudioAsync(f.Call);
        await PauseRateAsync(f.Media);
        var previous = RateLoop(f.Media)!;
        // A probe's padding can leave a large local buffer without queuing any media downstream.
        await using var pacer = new MediaSendPacer((_, _) => ValueTask.CompletedTask, () => 2_000_000);
        var loop = new SendRateLoop(pacer, new SendRateController(8_496), previous.Ladder);
        var transport = PendingProbe(f.Media);
        using var running = new CancellationTokenSource();
        var task = RunRateAsync(f.Media, loop, running.Token);
        try
        {
            loop.Signals.OnCongestionReport(new MediaCongestionData(), video: false, Environment.TickCount64);
            Assert.That(() => loop.LastDecision.HasValue, Is.True.After(1000, 20), "a probe does not pause rate windows");
            Assert.Multiple(() =>
            {
                Assert.That(loop.LastDecision!.Value.Signal, Is.EqualTo(RateSignal.Normal));
                Assert.That(loop.LastDecision.Value.DelayMs, Is.Zero, "only the padding-contaminated local queue is ignored");
                Assert.That(loop.Pacer.RateKbps, Is.GreaterThanOrEqualTo(8_496));
            });
            loop.Controller.Reset(200);
            loop.Signals.OnCongestionReport(new MediaCongestionData(), video: false, Environment.TickCount64);
            Assert.That(() => loop.LastDecision!.Value.AudioKbps, Is.EqualTo(24).After(1000, 20));
            Assert.That(() => f.Js.Calls, Does.Contain("reconfigureBitrate").After(1000, 20), "audio adaptation applies while the probe is pending");
            Assert.That(transport.Probing, Is.True);
        }
        finally
        {
            await running.CancelAsync();
            await task;
            typeof(MediaTransportClient).GetField("_probe", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(transport, null);
        }
    }

    private static async Task PauseRateAsync(BoltMediaService media)
    {
        var cts = (CancellationTokenSource)typeof(BoltMediaService).GetField("_rateCts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(media)!;
        await cts.CancelAsync();
        await (Task)typeof(BoltMediaService).GetField("_rateTask", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(media)!;
    }

    private static MediaTransportClient PendingProbe(BoltMediaService media)
    {
        var transport = (MediaTransportClient)typeof(BoltMediaService).GetField("_transport", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(media)!;
        typeof(MediaTransportClient).GetField("_probe", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(transport, new LinkProbe());
        return transport;
    }

    private static Task RunRateAsync(BoltMediaService media, SendRateLoop loop, CancellationToken ct) =>
        (Task)typeof(BoltMediaService).GetMethod("RateLoopAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(media, [loop, ct])!;

    [Test]
    public async Task LostSocket_DoesNotEndAHostedSFrameCall()
    {
        await using var f = await Fixture.CreateAsync();
        RaiseDisconnected(f.First);
        await Task.Delay(200);
        Assert.Multiple(() =>
        {
            Assert.That(f.Js.Calls, Does.Not.Contain("dispose"), "the SFrame session and its sender counter live on");
            Assert.That(f.Js.Calls, Does.Not.Contain("stopCapture"), "the microphone is not released by a network blip");
            Assert.That(f.Media.HasTransport, Is.True, "the host, not the socket, decides when to let go");
        });
    }

    [Test]
    public async Task Resume_SwapsTransport_KeepsTheEpoch_AndRepublishesAFreshStream()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Media.StartHostedAudioAsync(f.Call);
        var before = f.FirstTransport.AudioConfigs();
        Assert.That(before, Has.Count.EqualTo(1));

        await f.Media.SuspendTransportAsync();
        Assert.Multiple(() =>
        {
            Assert.That(f.Media.HasTransport, Is.False);
            Assert.That(f.Media.LastInboundTick, Is.Null);
            Assert.That(f.Media.IsSFrameReady, Is.True, "the epoch stays active across the swap: no rekey, no nonce reuse");
        });
        Assert.That(await f.Media.SendHeartbeatAsync(f.Call, 1), Is.False, "nothing to probe while detached");

        var (second, transport) = Fixture.Client();
        f.Media.AttachTransport(second);
        Assert.Throws<InvalidOperationException>(() => f.Media.AttachTransport(second), "one transport at a time");
        await f.Media.JoinHostedGroupAsync(f.Call);
        await f.Media.StartHostedAudioAsync(f.Call);

        var after = transport.AudioConfigs();
        Assert.Multiple(() =>
        {
            Assert.That(after, Has.Count.EqualTo(1), "the audio stream is published again on the new connection");
            Assert.That(after[0], Is.Not.EqualTo(before[0]), "as a new stream, so every receiver starts it fresh");
            Assert.That(f.Js.Calls.Count(x => x == "createSession"), Is.EqualTo(1), "on the same SFrame session");
            Assert.That(f.Js.Calls.Count(x => x == "installEpoch"), Is.EqualTo(1), "with the epoch it installed once");
        });
        Assert.That(await f.Media.SendHeartbeatAsync(f.Call, 2), Is.True);
        await second.DisposeAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task HevcRuntimeUpgrade_RefusedAt60_RetriesTheSameResolutionAt30_AndCapsFutureClimbs(bool alreadyAt30)
    {
        await using var f = await Fixture.CreateAsync();
        await f.Media.StartHostedAudioAsync(f.Call);
        await f.Media.StartVideoAsync(f.Call, VideoCodec.Hevc, 1440, preferredFramerate: 60);
        typeof(BoltMediaService).GetMethod("StopAdaptationLoop", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(f.Media, null);
        typeof(BoltMediaService).GetField("_videoLoop", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(f.Media, new CancellationTokenSource());
        var adaptation = new VideoAdaptation(VideoAdaptation.IndexForHeight(1440), 60);
        adaptation.SetCeiling(1440);
        adaptation.Rates.Place(15_000, 0, false);
        adaptation.Rates.Place(15_000, VideoRateLadder.FastUpHoldMs, false);
        Assert.That(adaptation.Current!.Value.Framerate, Is.EqualTo(60));
        typeof(BoltMediaService).GetField("_adaptation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(f.Media, adaptation);
        f.Js.RejectVideo60 = true;
        f.Js.Video30Unchanged = alreadyAt30;
        var notified = new List<VideoTier?>();
        f.Media.OnVideoTierChanged += notified.Add;
        var apply = typeof(BoltMediaService).GetMethod("ApplyVideoAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)apply.Invoke(f.Media, [new SendRateTick(default, adaptation.Rates.Current, false, false, null, default)])!;
        var changed = (VideoTier?)typeof(BoltMediaService).GetField("_appliedTier", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Media);
        Assert.Multiple(() =>
        {
            Assert.That(f.Js.AppliedVideoTiers.Select(x => x.Framerate), Is.EqualTo(new[] { 60, 30 }));
            Assert.That(changed!.Value.Height, Is.EqualTo(1440));
            Assert.That(changed.Value.Framerate, Is.EqualTo(30));
            Assert.That(notified, Is.EqualTo(new[] { changed }), "report the actual accepted tier even when it was already configured");
            Assert.That(adaptation.Rates.Allow60, Is.False, "subsequent rate windows retain the native encoder limit");
            Assert.That(f.Media.IsCameraOn, Is.True);
            Assert.That(f.Js.Calls, Does.Not.Contain("stopCapture"));
        });
    }

    [Test]
    public async Task Diagnostics_MergeRecoveryByStream_AndExposeTheLatestBudget()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Media.StartHostedAudioAsync(f.Call);
        await f.Media.StartVideoAsync(f.Call, VideoCodec.H264, 720);
        var incoming = Guid.NewGuid();
        var untracked = Guid.NewGuid();
        f.Js.VideoSnapshot = new VideoDiagnostics { Remotes = [new() { StreamId = incoming, RenderedFps = 27 }, new() { StreamId = untracked }] };
        var buffer = new VideoRecoveryBuffer();
        buffer.Configure(true, 500);
        buffer.Push(1, VideoFrameFragments.Split(new byte[10], 1, 0, true)[0], 0, []);
        var assemblers = (Dictionary<Guid, VideoRecoveryBuffer>)typeof(BoltMediaService)
            .GetField("_videoAssemblers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Media)!;
        assemblers.Add(incoming, buffer);
        var loop = RateLoop(f.Media)!;
        loop.Tick(Environment.TickCount64);
        var snapshot = await f.Media.GetVideoDiagnosticsAsync(true);
        Assert.Multiple(() =>
        {
            Assert.That(snapshot!.Remotes[0].RenderedFps, Is.EqualTo(27), "browser samples remain intact");
            Assert.That(snapshot.Remotes[0].Recovery!.StreamId, Is.EqualTo(incoming));
            Assert.That(snapshot.Remotes[0].Recovery!.Fragments, Is.EqualTo(1));
            Assert.That(snapshot.Remotes[0].Recovery!.RecoveryMs, Is.EqualTo(1050));
            Assert.That(snapshot.Remotes[1].Recovery, Is.Null, "never attribute another stream's loss");
            Assert.That(snapshot.TotalBudgetKbps, Is.EqualTo(loop.LastDecision!.Value.TotalKbps));
            Assert.That(snapshot.Congestion, Is.EqualTo(loop.LastDecision!.Value.Signal.ToString()));
        });
    }

    [Test]
    public async Task Resume_RepublishesTheCameraWithoutReopeningIt_AndStartsOnAKeyframe()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Media.StartHostedAudioAsync(f.Call);
        await f.Media.StartVideoAsync(f.Call, VideoCodec.H264, 720);
        var first = f.FirstTransport.Configs(MediaType.Video);
        Assert.That(first, Has.Count.EqualTo(1));

        await f.Media.SuspendTransportAsync();
        Assert.That(await f.Media.ResumeVideoStreamAsync(f.Call), Is.False, "no transport, nothing to publish on");
        var (second, transport) = Fixture.Client();
        f.Media.AttachTransport(second);
        await f.Media.JoinHostedGroupAsync(f.Call);
        await f.Media.StartHostedAudioAsync(f.Call);
        var opened = f.Js.Calls.Count(x => x == "startCapture");
        var keyframes = f.Js.Calls.Count(x => x == "requestKeyframe");

        Assert.That(await f.Media.ResumeVideoStreamAsync(f.Call), Is.True);
        Assert.That(await f.Media.ResumeVideoStreamAsync(f.Call), Is.False, "published once");
        var again = transport.Configs(MediaType.Video);
        Assert.Multiple(() =>
        {
            Assert.That(again, Has.Count.EqualTo(1));
            Assert.That(again[0], Is.Not.EqualTo(first[0]), "a new stream on the new connection");
            Assert.That(f.Js.Calls.Count(x => x == "startCapture"), Is.EqualTo(opened), "the camera was never closed, so it is not reopened");
            Assert.That(f.Js.Calls.Count(x => x == "requestKeyframe"), Is.GreaterThan(keyframes), "receivers start on a keyframe");
            Assert.That(f.Media.IsCameraOn, Is.True);
        });
        await second.DisposeAsync();
    }

    /// <summary>
    /// Phases 1 and 2 together. The pacer sends on its transport's client, so a resume must bring a new one:
    /// otherwise every frame after a resume goes to the dead connection. The new rate loop starts from the old
    /// path's stable rate (halved, above the start tier), not from its peak, with fresh signals and state.
    /// </summary>
    [Test]
    public async Task Resume_SendsOnTheNewTransport_AndRestartsTheRateFromTheOldPathsStableRate()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Media.StartHostedAudioAsync(f.Call);
        var firstLoop = RateLoop(f.Media)!;
        await EncodeAudioAsync(f.Media);
        Assert.That(() => f.FirstTransport.MediaFrames(), Is.GreaterThan(0).After(2000, 10), "audio flows on the first transport");

        await f.Media.SuspendTransportAsync();
        Assert.That(RateLoop(f.Media), Is.Null, "the lost transport's pacer and rate loop are gone with it");
        var (second, transport) = Fixture.Client();
        f.Media.AttachTransport(second);
        await f.Media.JoinHostedGroupAsync(f.Call);
        await f.Media.StartHostedAudioAsync(f.Call);
        var oldFrames = f.FirstTransport.MediaFrames();
        await EncodeAudioAsync(f.Media);

        var loop = RateLoop(f.Media)!;
        Assert.Multiple(() =>
        {
            Assert.That(() => transport.MediaFrames(), Is.GreaterThan(0).After(2000, 10), "audio flows on the resumed transport");
            Assert.That(f.FirstTransport.MediaFrames(), Is.EqualTo(oldFrames), "and nothing more on the dead one");
            Assert.That(loop, Is.Not.SameAs(firstLoop));
            Assert.That(loop.Pacer, Is.Not.SameAs(firstLoop.Pacer), "a new pacer, on the new client");
            Assert.That(loop.Controller, Is.Not.SameAs(firstLoop.Controller), "a new controller: no delay history from the old path");
            Assert.That(loop.Controller.EstimateKbps,
                Is.EqualTo(SendRateController.RestartKbps(firstLoop.Controller.StableKbps, firstLoop.Controller.Options, 84)),
                "starting from the old path's stable rate");
        });
        await second.DisposeAsync();
    }

    [Test]
    public async Task Resume_PlacesThePictureBeforeTheFirstKeyframe_AndForcesThatKeyframe()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Media.StartHostedAudioAsync(f.Call);
        await f.Media.StartVideoAsync(f.Call, VideoCodec.H264, 1080);
        // The old path had climbed to 1080p (its rate loop paused, so nothing moves the ladder meanwhile).
        var rates = (CancellationTokenSource)typeof(BoltMediaService).GetField("_rateCts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Media)!;
        await rates.CancelAsync();
        await (Task)typeof(BoltMediaService).GetField("_rateTask", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Media)!;
        var ladder = RateLoop(f.Media)!.Ladder;
        for (long now = 0; now < 60_000; now += 250) ladder.Place(5_000, now, false);
        Assert.That(ladder.Current.Rung.Height, Is.EqualTo(1080));

        await f.Media.SuspendTransportAsync();
        var (second, transport) = Fixture.Client();
        f.Media.AttachTransport(second);
        await f.Media.JoinHostedGroupAsync(f.Call);
        await f.Media.StartHostedAudioAsync(f.Call);
        var keyframes = f.Js.Calls.Count(x => x == "requestKeyframe");
        Assert.That(await f.Media.ResumeVideoStreamAsync(f.Call), Is.True);

        var restart = RateLoop(f.Media)!.Controller.EstimateKbps;
        Assert.Multiple(() =>
        {
            Assert.That(f.Media.ActiveVideoTier?.Height, Is.LessThan(1080), "not the old path's peak");
            Assert.That(f.Media.ActiveVideoTier?.BitrateKbps, Is.LessThanOrEqualTo(Math.Max(restart - 84, 120)), "the picture fits the restart estimate");
            Assert.That(f.Js.Calls.Count(x => x == "requestKeyframe"), Is.GreaterThan(keyframes), "a keyframe is asked for at once");
            Assert.That(RateLoop(f.Media)!.Pacer.WouldAccept(false, 0), Is.False, "and nothing but it goes out first");
            Assert.That(RateLoop(f.Media)!.Pacer.WouldAccept(true, 0), Is.True);
        });
        await second.DisposeAsync();
    }

    [Test]
    public async Task SendPathPoor_UsesTheControllersOwnHighDelayThreshold()
    {
        await using var f = await Fixture.CreateAsync();
        Assert.That(f.Media.SendPathPoor, Is.False, "no send path, no verdict");
        await f.Media.StartHostedAudioAsync(f.Call);
        var high = RateLoop(f.Media)!.Controller.Options.HighDelayMs;
        var rate = typeof(BoltMediaService).GetProperty(nameof(BoltMediaService.SendRate))!;
        rate.SetValue(f.Media, new SendRateDecision(300, 32, 180, false, RateSignal.Overuse, high));
        Assert.That(f.Media.SendPathPoor, Is.True, "a queue the controller calls high is a poor connection");
        rate.SetValue(f.Media, new SendRateDecision(300, 32, 180, false, RateSignal.Hold, high - 1));
        Assert.That(f.Media.SendPathPoor, Is.False, "below it, it is the controller's business alone");
    }

    private static Bolt.Media.Congestion.SendRateLoop? RateLoop(BoltMediaService media) =>
        (Bolt.Media.Congestion.SendRateLoop?)typeof(BoltMediaService).GetField("_rateLoop", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(media);

    /// <summary>What the browser's Opus encoder hands over for one 20 ms packet.</summary>
    private static async Task EncodeAudioAsync(BoltMediaService media)
    {
        var handler = typeof(BoltMediaService).GetMethod("OnAudioEncodedForStream", BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (uint i = 1; i <= 3; i++) await (Task)handler.Invoke(media, [new byte[80], i * 960])!;
    }

    [Test]
    public async Task AttachTransport_RequiresWss()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Media.SuspendTransportAsync();
        await using var plain = new BoltClient(new Uri("ws://example.test/media"), "caller", "Test", new(), NullLogger.Instance);
        Assert.Throws<InvalidOperationException>(() => f.Media.AttachTransport(plain));
    }

    private static void RaiseDisconnected(BoltClient client) =>
        ((Action?)typeof(BoltClient).GetField("Disconnected", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client))?.Invoke();

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Call { get; } = Guid.NewGuid();
        public RecordingJs Js { get; } = new();
        public BoltMediaService Media { get; private set; } = null!;
        public BoltClient First { get; private set; } = null!;
        public Recording FirstTransport { get; private set; } = null!;
        private ServiceProvider provider = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture();
            var services = new ServiceCollection();
            services.AddSingleton<IJSRuntime>(f.Js);
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddBoltMediaBrowser(options => options.SecurityMode = MediaSecurityMode.AuthenticatedSFrame);
            f.provider = services.BuildServiceProvider();
            f.Media = f.provider.GetRequiredService<BoltMediaService>();
            (f.First, f.FirstTransport) = Client();
            await f.Media.InitializeAsync(f.First);
            var sender = $"yap-media-{f.Call:N}-{Guid.NewGuid():N}";
            await f.Media.ConfigureSFrameAsync(f.Call, sender);
            await f.Media.JoinHostedGroupAsync(f.Call);
            var local = new SFrameSenderKey(sender, "1", new byte[32]);
            var remote = new SFrameSenderKey($"yap-media-{f.Call:N}-{Guid.NewGuid():N}", "2", Enumerable.Repeat((byte)1, 32).ToArray());
            await f.Media.InstallSFrameEpochAsync("1", new string('a', 64), local, [remote]);
            await f.Media.ActivateSFrameEpochAsync("1", new string('a', 64));
            return f;
        }

        public static (BoltClient, Recording) Client()
        {
            var client = new BoltClient(new Uri("wss://example.test/media"), "caller", "Test", new(), NullLogger.Instance);
            var transport = new Recording();
            var connection = new BoltConnection(transport);
            connection.StartSendLoop(CancellationToken.None);
            ((List<BoltConnection>)typeof(BoltClient).GetField("_connections", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!).Add(connection);
            return (client, transport);
        }

        public async ValueTask DisposeAsync()
        {
            await provider.DisposeAsync();
            await First.DisposeAsync();
        }
    }

    public sealed class Recording : IBoltConnection
    {
        public ConcurrentQueue<byte[]> Sent { get; } = new();
        public List<Guid> AudioConfigs() => Configs(MediaType.Audio);
        public int MediaFrames() => Sent.Count(x => x.Length > 0 && x[0] == (byte)FrameType.MediaFrame);
        public List<Guid> Configs(MediaType type)
        {
            var deadline = DateTime.UtcNow.AddSeconds(2);
            bool Match(byte[] x) => BoltCodec.TryReadMediaConfig(x, out var c) && c.MediaType == type;
            while (DateTime.UtcNow < deadline && !Sent.Any(Match)) Thread.Sleep(10);
            return Sent.Where(Match)
                .Select(x => { BoltCodec.TryReadMediaConfig(x, out var c); return c.StreamId; }).ToList();
        }
        public bool SupportsDatagrams => false;
        public bool IsConnected => true;
        public BoltTransport TransportType => BoltTransport.WebSocket;
        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) { Sent.Enqueue(data.ToArray()); return ValueTask.CompletedTask; }
        public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
        { await Task.Delay(Timeout.Infinite, ct); return (0, true); }
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask CloseAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Records every interop call by name and hands itself out as any imported module or session.</summary>
    public sealed class RecordingJs : IJSInProcessRuntime, IJSObjectReference
    {
        public ConcurrentQueue<string> Calls { get; } = new();
        public VideoDiagnostics? VideoSnapshot { get; set; }
        public bool RejectVideo60 { get; set; }
        public bool Video30Unchanged { get; set; }
        public List<VideoTier> AppliedVideoTiers { get; } = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(Invoke<TValue>(identifier, args));
        public TValue Invoke<TValue>(string identifier, params object?[]? args)
        {
            Calls.Enqueue(identifier);
            if (identifier == "applyTier" && args is { Length: 4 })
            {
                var tier = new VideoTier((int)args[0]!, (int)args[1]!, (int)args[3]!, (int)args[2]!);
                AppliedVideoTiers.Add(tier);
                if (RejectVideo60 && tier.Framerate > 30) throw new JSException("Encoder creation error");
                if (Video30Unchanged && tier.Framerate == 30) return (TValue)(object)false;
            }
            object? value = typeof(TValue).IsAssignableFrom(typeof(RecordingJs)) ? this
                : typeof(TValue) == typeof(bool) ? true
                : typeof(TValue) == typeof(byte[]) ? new byte[16]
                : typeof(TValue) == typeof(VoiceCapabilities) ? new VoiceCapabilities(true, null, true)
                : typeof(TValue) == typeof(VideoDiagnostics) ? VideoSnapshot
                : typeof(TValue) == typeof(VideoCaptureState) ? CameraState()
                : typeof(TValue) == typeof(VideoSendStats) ? new VideoSendStats(30, 5000, 0, 0)
                : default(TValue);
            return (TValue)value!;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static VideoCaptureState CameraState() =>
            (VideoCaptureState)System.Text.Json.JsonSerializer.Deserialize("{\"capturing\":true,\"width\":426,\"height\":240}", typeof(VideoCaptureState),
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
