using System.Buffers;
using Bolt.Client;
using Bolt.Media;
using Bolt.Media.Congestion;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Bolt.Server;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// Phase 1 of call network resilience: temporal layers at the relay, the relay's congestion reports, the sender's
/// pacer (audio first, whole pictures), the wire formats that carry them, and NACK staying off over TCP.
/// </summary>
[CancelAfter(20_000)]
public sealed class BoltCallCongestionTests
{
    private static readonly Guid Audio = Guid.NewGuid(), Video = Guid.NewGuid();

    // ── Wire formats ──

    [Test]
    public void CongestionReport_RoundTrips_AndOnlyReadsItsOwnFrameType()
    {
        var report = new MediaCongestionData
        {
            StreamId = Video, Flags = MediaCongestionFlags.Limited | MediaCongestionFlags.BaseLayerLost, Receivers = 3,
            QueueDelayMs = 420, UplinkDelayMs = 35, AllowedKbps = 380, DroppedPictures = 7, LayerLimit = 1
        };
        var frame = Write(w => BoltCodec.WriteMediaCongestion(w, report));
        Assert.That(frame, Has.Length.EqualTo(BoltCodec.MediaCongestionSize));
        Assert.That(BoltCodec.TryReadMediaCongestion(frame, out var read), Is.True);
        Assert.That(read, Is.EqualTo(report));
        var keyRequest = Write(w => BoltCodec.WriteMediaKeyRequest(w, Video)).Concat(new byte[16]).ToArray();
        Assert.That(BoltCodec.TryReadMediaCongestion(keyRequest, out _), Is.False, "a frame of another type is not a report");
        frame[17] = 99;
        Assert.That(BoltCodec.TryReadMediaCongestion(frame, out _), Is.False, "an unknown report version is ignored");
    }

    [Test]
    public void ReceiverDelayReport_ExtendsFeedback_WithoutBreakingOldReaders()
    {
        var extended = Write(w => BoltCodec.WriteMediaFeedback(w, Video, 10, 2, 300, 0, QualityHint.Maintain, 240, 455));
        var plain = Write(w => BoltCodec.WriteMediaFeedback(w, Video, 10, 2, 300, 0, QualityHint.Maintain));
        Assert.Multiple(() =>
        {
            Assert.That(extended, Has.Length.EqualTo(BoltCodec.MediaFeedbackExtendedSize));
            Assert.That(BoltCodec.TryReadMediaFeedback(extended, out var a), Is.True);
            Assert.That((a.HasDelayReport, a.QueueDelayMs, a.ReceivedKbps, a.HighestSeqReceived), Is.EqualTo((true, (ushort)240, 455u, 10u)));
            Assert.That(BoltCodec.TryReadMediaFeedback(plain, out var b), Is.True);
            Assert.That(b.HasDelayReport, Is.False, "a phase 0 receiver's feedback carries no delay report");
            // A phase 0 sender reads the first 32 bytes; the extension is invisible to it.
            Assert.That(extended.AsSpan(0, BoltCodec.MediaFeedbackSize).SequenceEqual(plain), Is.True);
        });
    }

    [Test]
    public void TemporalLayer_TravelsInTheClearFlags_BesideTheKeyframeAndEncryptedBits()
    {
        var flags = MediaFrameFlags.WithTemporalLayer(MediaFrameFlags.Encrypted | MediaFrameFlags.Keyframe, 2);
        var frame = Write(w => BoltCodec.WriteMediaFrame(w, Video, 1, 90, flags, [1]));
        Assert.That(BoltCodec.TryReadMediaFrame(frame, out var header), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(header.TemporalLayer, Is.EqualTo(2));
            Assert.That(header.IsKeyframe, Is.True);
            Assert.That(header.IsEncrypted, Is.True);
            Assert.That(MediaFrameFlags.TemporalLayer(MediaFrameFlags.WithTemporalLayer(0xFF, 0)), Is.Zero);
            Assert.That(MediaFrameFlags.WithTemporalLayer(0, 9), Is.EqualTo(MediaFrameFlags.WithTemporalLayer(0, 3)), "clamped to two bits");
        });
    }

    [Test]
    public void QueuingDelayEstimator_ReportsGrowthAboveTheFloor_AndRestartsOnAWrap()
    {
        var estimator = new MediaQueuingDelayEstimator();
        // 20 ms audio frames: the first second arrives on time, the next frames each 100 ms later than the last.
        long now = 1_000;
        uint ts = 0;
        for (var i = 0; i < 50; i++) { estimator.Observe(ts, 48, now); ts += 960; now += 20; }
        Assert.That(estimator.DelayMs, Is.LessThanOrEqualTo(1));
        for (var i = 0; i < 5; i++) { estimator.Observe(ts, 48, now); ts += 960; now += 120; }
        Assert.That(estimator.DelayMs, Is.InRange(390, 410));
        // A timestamp jump of hours is a restart, not four hours of delay.
        estimator.Observe(ts + 48 * 3_600_000u, 48, now);
        Assert.That(estimator.DelayMs, Is.Zero);
    }

    // ── Relay queue: temporal layers ──

    [Test]
    public void Layers_ACongestedReceiverShedsTheTopLayerFirst_ThenAllEnhancement_AndKeepsTheBase()
    {
        var now = 0L;
        // 1000 ms budget: the top layer goes at 200 ms of queue, every enhancement layer at 400 ms.
        var queue = new BoltMediaSendQueue(new BoltMediaSendQueueOptions { VideoMaxQueueDelayMs = 1_000, VideoMaxQueuedBytes = 1 << 20 }, () => now);
        var stream = new LayeredStream(queue, Video);
        stream.Offer(keyframe: true, layer: 0);
        for (var i = 0; i < 3; i++) stream.Offer(false, new[] { 2, 1, 2 }[i]);
        Assert.That(stream.Accepted, Is.EqualTo(4), "no pressure, every layer goes");
        now = 250; // nothing drained: the oldest queued picture is 250 ms old
        stream.Offer(false, 0);
        stream.Offer(false, 2);
        Assert.That(stream.LastAccepted, Is.False, "past the first threshold the top layer is shed");
        stream.Offer(false, 1);
        Assert.That(stream.LastAccepted, Is.True, "the middle layer still goes");
        now = 450;
        stream.Offer(false, 2);
        stream.Offer(false, 0);
        Assert.That(stream.LastAccepted, Is.True, "the base layer goes whatever the enhancement layers do");
        stream.Offer(false, 1);
        Assert.That(stream.LastAccepted, Is.False, "past the second threshold every enhancement layer is shed");
        Assert.That(stream.KeyframeRequests, Is.Zero, "shedding enhancement layers never needs a keyframe");
        Assert.That(queue.LayerDrops, Is.EqualTo(3));
    }

    [Test]
    public void Layers_ComeBackOnlyAtABasePicture_SoEverythingForwardedDecodes()
    {
        var now = 0L;
        var queue = new BoltMediaSendQueue(new BoltMediaSendQueueOptions { VideoMaxQueueDelayMs = 1_000, VideoMaxQueuedBytes = 1 << 20 }, () => now);
        var stream = new LayeredStream(queue, Video);
        var random = new Random(7);
        // An L1T3 stream (0,2,1,2) through a receiver whose link comes and goes.
        stream.Offer(true, 0);
        for (var picture = 1; picture < 2_000; picture++)
        {
            now += 33;
            if (random.Next(3) == 0) queue.DrainAll(); // the link takes everything now and then
            stream.Offer(false, new[] { 0, 2, 1, 2 }[picture % 4]);
        }
        queue.DrainAll();
        Assert.That(stream.Undecodable, Is.Zero, "a forwarded picture never refers to one that was shed");
        Assert.That(stream.Accepted, Is.LessThan(2_000), "the pressure did shed pictures");
        Assert.That(queue.LayerDrops, Is.GreaterThan(0));
    }

    [Test]
    public void Layers_EveryFragmentOfAPictureSharesItsFate()
    {
        var now = 0L;
        var queue = new BoltMediaSendQueue(new BoltMediaSendQueueOptions { VideoMaxQueueDelayMs = 1_000, VideoMaxQueuedBytes = 1 << 20 }, () => now);
        queue.TryEnqueue(Frame(1), BoltMediaLane.Video, Video, 1, keyStart: true, picture: 90, layer: 0);
        now = 300;
        // A top-layer picture of three fragments arrives while the queue is past the shedding threshold.
        var fragments = Enumerable.Range(0, 3)
            .Select(i => queue.TryEnqueue(Frame(2), BoltMediaLane.Video, Video, (uint)(2 + i), keyStart: false, picture: 180, layer: 2).Queued)
            .ToArray();
        Assert.That(fragments, Is.EqualTo(new[] { false, false, false }), "half a picture is never forwarded");
        Assert.That(queue.TryEnqueue(Frame(3), BoltMediaLane.Video, Video, 5, keyStart: false, picture: 270, layer: 0).Queued, Is.True);
    }

    [Test]
    public void Layers_AStreamWithoutLayersStillDropsUntilAKeyframe()
    {
        var queue = new BoltMediaSendQueue(new BoltMediaSendQueueOptions { VideoMaxQueuedBytes = 300, VideoMaxQueueDelayMs = 60_000 });
        queue.TryEnqueue(Frame(1, 100), BoltMediaLane.Video, Video, 1, keyStart: true, picture: 1);
        queue.TryEnqueue(Frame(2, 100), BoltMediaLane.Video, Video, 2, keyStart: false, picture: 2);
        queue.TryEnqueue(Frame(3, 100), BoltMediaLane.Video, Video, 3, keyStart: false, picture: 3);
        var overflow = queue.TryEnqueue(Frame(4, 100), BoltMediaLane.Video, Video, 4, keyStart: false, picture: 4);
        Assert.That(overflow, Is.EqualTo(new BoltMediaEnqueueResult(false, true)), "the base layer lost: a keyframe is needed");
        Assert.That(queue.Snapshot(Video).StreamBaseLosses, Is.EqualTo(1));
    }

    [Test]
    public void Snapshot_MeasuresTheDrainRateWhileBacklogged_AndForgetsItWhenIdle()
    {
        var now = 0L;
        var queue = new BoltMediaSendQueue(new BoltMediaSendQueueOptions(), () => now);
        for (uint i = 0; i < 20; i++) queue.TryEnqueue(Frame(1, 1000), BoltMediaLane.Audio, Audio, i + 1, keyStart: false);
        // The link takes one 1000-byte frame every 20 ms: 400 kbps.
        for (var i = 0; i < 20; i++) { Assert.That(queue.TryDequeue(out var item), Is.True); BoltMediaSendQueue.Release(item); now += 20; }
        var busy = queue.Snapshot(Audio);
        Assert.That(busy.DeliveryKbps, Is.InRange(360, 440));
        now += 5_000;
        Assert.That(queue.Snapshot(Audio).DeliveryKbps, Is.Zero, "an old measurement is not a capacity");
    }

    // ── Relay congestion reports ──

    [Test]
    public void Reporter_TakesTheWorstReceiver_AndThisStreamsShareOfItsDrain()
    {
        var now = 0L;
        var slow = new BoltHubConnection(new NullConnection(), 64, 0, 1 << 20, 0, new BoltMediaSendQueueOptions());
        var fast = new BoltHubConnection(new NullConnection(), 64, 0, 1 << 20, 0, new BoltMediaSendQueueOptions());
        var reporter = new MediaCongestionReporter();
        // First report: a baseline.
        Assert.That(reporter.TryBuild(Video, true, [slow, fast], now), Is.Not.Null);
        // The slow receiver backs up; the fast one drains everything.
        for (uint i = 1; i <= 40; i++)
        {
            slow.TryEnqueueMedia(Frame(1, 1000), BoltMediaLane.Video, Video, i, keyStart: i == 1, picture: i, layer: 0);
            slow.TryEnqueueMedia(Frame(2, 250), BoltMediaLane.Audio, Audio, i);
            fast.TryEnqueueMedia(Frame(1, 1000), BoltMediaLane.Video, Video, i, keyStart: i == 1, picture: i, layer: 0);
        }
        Thread.Sleep(80); // the slow receiver's oldest queued media ages
        now += 300;
        var built = reporter.TryBuild(Video, true, [slow, fast], now);
        Assert.That(built, Is.Not.Null);
        Assert.That(BoltCodec.TryReadMediaCongestion(built, out var report), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(report.Receivers, Is.EqualTo(2));
            Assert.That(report.QueueDelayMs, Is.GreaterThanOrEqualTo(60), "the slow receiver's queue");
            Assert.That(report.LayerLimit, Is.EqualTo(3));
            Assert.That(reporter.TryBuild(Video, true, [slow, fast], now + 10), Is.Null, "reports are rate limited");
        });
    }

    [Test]
    public void Reporter_SaysNothingWithoutReceivers()
    {
        Assert.That(new MediaCongestionReporter().TryBuild(Video, true, [], 0), Is.Null);
    }

    // ── Sender pacer ──

    [Test]
    public void Pacer_SendsAudioBeforeVideo_AndFinishesAPictureOnceStarted()
    {
        var pacer = new MediaSendPacer((_, _) => ValueTask.CompletedTask);
        pacer.EnqueueVideo(Picture(true, 0, 1, 3));
        Assert.That(Take(pacer), Is.EqualTo((1, false)), "the first fragment of a picture");
        pacer.EnqueueVideo(Picture(false, 0, 2, 1));
        pacer.EnqueueAudio([9]);
        Assert.That(Take(pacer), Is.EqualTo((9, true)), "audio overtakes the rest of the picture");
        Assert.That(new[] { Take(pacer), Take(pacer), Take(pacer) }, Is.EqualTo(new[] { (1, false), (1, false), (2, false) }),
            "and the started picture finishes before the next one");
    }

    [Test]
    public void Pacer_OverBudget_ShedsEnhancementPicturesFirst_ThenWaitsForAKeyframe()
    {
        var now = 0L;
        var pacer = new MediaSendPacer((_, _) => ValueTask.CompletedTask,
            options: new MediaSendPacerOptions { VideoMaxQueuedBytes = 1_000, VideoMaxDelayMs = 60_000, LayerShedFraction = 10 }, clock: () => now);
        var keyframes = 0;
        pacer.KeyframeNeeded += () => keyframes++;
        Assert.That(pacer.EnqueueVideo(Picture(true, 0, 1, 1, 300)), Is.True);
        Assert.That(pacer.EnqueueVideo(Picture(false, 2, 2, 1, 300)), Is.True);
        Assert.That(pacer.EnqueueVideo(Picture(false, 1, 3, 1, 300)), Is.True);
        // Over budget: the queued enhancement pictures make room for a base picture.
        Assert.That(pacer.EnqueueVideo(Picture(false, 0, 4, 1, 300)), Is.True);
        Assert.That(keyframes, Is.Zero);
        Assert.That(pacer.WouldAccept(false, 2), Is.False, "no enhancement picture until the next base picture");
        // Now even base pictures overflow: the queue waits for a keyframe and asks for one.
        Assert.That(pacer.EnqueueVideo(Picture(false, 0, 5, 1, 900)), Is.False);
        Assert.That(keyframes, Is.EqualTo(1));
        Assert.That(pacer.IsAwaitingKeyframe, Is.True);
        Assert.That(pacer.EnqueueVideo(Picture(false, 0, 6, 1, 10)), Is.False, "a delta without its reference is useless");
        Assert.That(pacer.EnqueueVideo(Picture(true, 0, 7, 1, 10)), Is.True);
        Assert.That(pacer.IsAwaitingKeyframe, Is.False);
        var sample = pacer.Sample();
        Assert.That(sample.BaseLosses, Is.EqualTo(1));
    }

    [Test]
    public void Pacer_APictureLostBeforeIt_OnlyWithholdsItsLayerUntilTheNextBase()
    {
        var pacer = new MediaSendPacer((_, _) => ValueTask.CompletedTask);
        var keyframes = 0;
        pacer.KeyframeNeeded += () => keyframes++;
        pacer.EnqueueVideo(Picture(true, 0, 1, 1));
        pacer.NotePictureLost(1);
        Assert.That((pacer.WouldAccept(false, 1), pacer.WouldAccept(false, 2), pacer.WouldAccept(false, 0)), Is.EqualTo((false, false, true)));
        Assert.That(keyframes, Is.Zero);
        pacer.NotePictureLost(0);
        Assert.That(keyframes, Is.EqualTo(1), "a lost base picture needs a keyframe");
        Assert.That(pacer.WouldAccept(false, 0), Is.False);
    }

    [Test]
    public async Task Pacer_HoldsMediaAboveAFullTransport_SoAudioStillOvertakes()
    {
        var sent = new List<byte>();
        var backlog = 100_000L;
        var pacer = new MediaSendPacer((frame, _) => { lock (sent) sent.Add(frame.Span[0]); return ValueTask.CompletedTask; }, () => Interlocked.Read(ref backlog));
        pacer.Start();
        pacer.EnqueueVideo(Picture(true, 0, 1, 1));
        await Task.Delay(100);
        lock (sent) Assert.That(sent, Is.Empty, "nothing goes below a full transport");
        pacer.EnqueueAudio([9]);
        Interlocked.Exchange(ref backlog, 0);
        for (var i = 0; i < 100 && sent.Count < 2; i++) await Task.Delay(10);
        lock (sent) Assert.That(sent, Is.EqualTo(new byte[] { 9, 1 }), "the audio that arrived meanwhile goes first");
        var sample = pacer.Sample();
        Assert.That(sample.CapacityKbps, Is.GreaterThanOrEqualTo(0));
        await pacer.DisposeAsync();
    }

    [Test]
    public void Pacer_DropsAudioThatWaitedTooLong()
    {
        var now = 0L;
        var pacer = new MediaSendPacer((_, _) => ValueTask.CompletedTask, options: new MediaSendPacerOptions { AudioMaxDelayMs = 300 }, clock: () => now);
        pacer.EnqueueAudio([1]);
        now = 200;
        pacer.EnqueueAudio([2]);
        now = 400;
        Assert.That(Take(pacer), Is.EqualTo((2, true)), "late speech is worse than a gap");
        Assert.That(pacer.Sample().DroppedAudio, Is.EqualTo(1));
    }

    // ── Sender signals ──

    [Test]
    public void Signals_AddTheRelaysShares_AndKeepASilentReceiversLastWord()
    {
        var signals = new SendPathSignals();
        signals.OnCongestionReport(new MediaCongestionData { StreamId = Video, Flags = MediaCongestionFlags.Limited | MediaCongestionFlags.Dropping,
            QueueDelayMs = 300, AllowedKbps = 350 }, video: true, 0);
        signals.OnCongestionReport(new MediaCongestionData { StreamId = Audio, QueueDelayMs = 280 }, video: false, 0);
        signals.OnReceiverFeedback(Feedback(Video, 500, 330), video: true, 0);
        var (relay, receiver) = signals.Take(100, sentVideoKbps: 600, sentAudioKbps: 60);
        Assert.Multiple(() =>
        {
            Assert.That(relay!.Value.CapacityKbps, Is.EqualTo(350 + 60), "an unlimited audio route counts at what it sends");
            Assert.That(relay.Value.Dropping, Is.True);
            Assert.That(relay.Value.QueueDelayMs, Is.EqualTo(300));
            Assert.That(receiver!.Value.QueueDelayMs, Is.EqualTo(500));
            Assert.That(receiver.Value.ReceivedKbps, Is.EqualTo(330 + 60));
        });
        Assert.That(signals.Take(200, 600, 60).Relay!.Value.Dropping, Is.False, "a drop is reported once");
        var (_, silent) = signals.Take(5_000, 600, 60);
        Assert.That(silent!.Value.ReceivedAtMs, Is.Zero, "a receiver that went quiet keeps its last report, with its age");
        Assert.That(signals.Take(20_000, 600, 60).Receiver, Is.Null);
    }

    [Test]
    public void Signals_IgnoreFeedbackWithoutADelayReport()
    {
        var signals = new SendPathSignals();
        var plain = new MediaFeedbackData { StreamId = Video };
        signals.OnReceiverFeedback(plain, true, 0);
        Assert.That(signals.Take(10, 100, 50).Receiver, Is.Null);
    }

    // ── Streams over TCP ──

    [Test]
    public void Nack_IsOffOverAWebSocket()
    {
        var connection = new BoltConnection(new NullConnection());
        var stream = new BoltMediaStream(connection, Guid.NewGuid(), Guid.NewGuid(), isAudio: false);
        stream.EnableNack();
        Assert.Multiple(() =>
        {
            Assert.That(stream.IsReliableTransport, Is.True);
            Assert.That(typeof(BoltMediaStream).GetField("_nackTracker", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(stream), Is.Null, "TCP retransmits; NACKing a deliberate drop only adds load");
            Assert.That(MediaTransportPolicy.IsReliable(BoltTransport.WebSocket), Is.True);
            Assert.That(MediaTransportPolicy.IsReliable(BoltTransport.WebTransport), Is.False);
        });
    }

    [Test]
    public async Task SendPicture_MarksEveryFragmentWithItsLayer_AndGoesThroughThePacerAsOneUnit()
    {
        var connection = new BoltConnection(new NullConnection());
        var stream = new BoltMediaStream(connection, Video, Guid.NewGuid(), isAudio: false);
        var sent = new List<byte[]>();
        var pacer = new MediaSendPacer((frame, _) => { sent.Add(frame.ToArray()); return ValueTask.CompletedTask; });
        stream.SetPacer(pacer);
        Assert.That(await stream.SendPictureAsync([[1], [2], [3]], isKeyframe: false, timestamp: 900, temporalLayer: 2), Is.True);
        while (pacer.TryTake(out var frame, out _)) sent.Add(frame);
        var headers = sent.Select(x => { BoltCodec.TryReadMediaFrame(x, out var h); return h; }).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(headers, Has.Length.EqualTo(3));
            Assert.That(headers.All(h => h.TemporalLayer == 2 && h.Timestamp == 900 && !h.IsKeyframe), Is.True);
            Assert.That(headers.Select(h => h.SequenceNumber), Is.EqualTo(new uint[] { 0, 1, 2 }));
        });
        await stream.DisposeAsync();
        await pacer.DisposeAsync();
    }

    // ── helpers ──

    private static (int Marker, bool Audio) Take(MediaSendPacer pacer) =>
        pacer.TryTake(out var frame, out var audio) ? (frame[0], audio) : (-1, false);

    private static PacedPicture Picture(bool keyframe, int layer, byte marker, int fragments, int bytes = 8) =>
        new(Enumerable.Range(0, fragments).Select(_ => Frame(marker, bytes)).ToArray(), keyframe, layer);

    private static MediaFeedbackData Feedback(Guid stream, int delayMs, int kbps)
    {
        var frame = Write(w => BoltCodec.WriteMediaFeedback(w, stream, 1, 0, 0, 0, QualityHint.Maintain, (ushort)delayMs, (uint)kbps));
        BoltCodec.TryReadMediaFeedback(frame, out var feedback);
        return feedback;
    }

    private static byte[] Frame(byte marker, int length = 8)
    {
        var frame = new byte[length];
        frame[0] = marker;
        return frame;
    }

    private static byte[] Write(Action<IBufferWriter<byte>> write)
    {
        var writer = new ArrayBufferWriter<byte>();
        write(writer);
        return writer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// An L1T3-shaped stream offered to one receiver's queue, one fragment per picture, that checks every forwarded
    /// picture can be decoded: a layer-L picture refers to the latest lower-layer picture (layer 0 to the latest base).
    /// </summary>
    private sealed class LayeredStream(BoltMediaSendQueue queue, Guid stream)
    {
        private uint _sequence, _picture;
        private uint _lastBase, _lastLayer1;
        private readonly HashSet<uint> _forwarded = [];
        public int Accepted, KeyframeRequests, Undecodable;
        public bool LastAccepted;

        public void Offer(bool keyframe, int layer)
        {
            _picture++;
            var reference = keyframe ? 0 : layer == 0 ? _lastBase : layer == 1 ? _lastBase : Math.Max(_lastBase, _lastLayer1);
            if (keyframe || layer == 0) { _lastBase = _picture; _lastLayer1 = 0; }
            else if (layer == 1) _lastLayer1 = _picture;
            var result = queue.TryEnqueue(Frame(1, 2_000), BoltMediaLane.Video, stream, ++_sequence, keyframe, picture: _picture * 3000, layer: layer);
            LastAccepted = result.Queued;
            if (result.RequestKeyframe) KeyframeRequests++;
            if (!result.Queued) return;
            Accepted++;
            if (!keyframe && !_forwarded.Contains(reference)) Undecodable++;
            _forwarded.Add(_picture);
        }
    }

    private sealed class NullConnection : IBoltConnection
    {
        public bool SupportsDatagrams => false;
        public bool IsConnected => true;
        public BoltTransport TransportType => BoltTransport.WebSocket;
        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            new(Task.Delay(Timeout.Infinite, ct).ContinueWith(_ => (0, true), TaskScheduler.Default));
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask CloseAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal static class BoltMediaSendQueueTestExtensions
{
    public static void DrainAll(this BoltMediaSendQueue queue)
    {
        while (queue.TryDequeue(out var item)) BoltMediaSendQueue.Release(item);
    }
}
