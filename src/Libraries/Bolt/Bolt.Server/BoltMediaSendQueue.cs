using System.Buffers;

namespace Bolt.Server;

/// <summary>
/// Per-receiver media limits for the relay. Media never waits for a slow receiver: when these
/// budgets are exceeded the relay drops, video first and whole pictures at a time, and asks the
/// sender for a fresh keyframe. The receiving connection itself is only retired by the transport
/// progress watchdog (<see cref="BoltServerOptions.TransportSendStallTimeoutMs"/>).
/// </summary>
public sealed class BoltMediaSendQueueOptions
{
    /// <summary>
    /// Queued audio older than this is dropped, oldest first. Default: 1000 ms. On a 500-1000 ms RTT path one TCP
    /// retransmission stalls the link for about a round trip, and speech that waited through it is still worth
    /// playing; the receiver's playout buffer, not the relay, decides what is too late.
    /// </summary>
    public int AudioMaxQueueDelayMs { get; set; } = 1000;

    /// <summary>Byte budget for queued audio per receiver. Default: 64 KiB (several seconds of 32 kbps Opus).</summary>
    public int AudioMaxQueuedBytes { get; set; } = 64 * 1024;

    /// <summary>
    /// When the oldest queued video is older than this, the congested stream's queued pictures are
    /// discarded and that receiver waits for the next keyframe. Default: 1500 ms.
    /// </summary>
    public int VideoMaxQueueDelayMs { get; set; } = 1500;

    /// <summary>Byte budget for queued video per receiver. Default: 512 KiB (two 1080p keyframes).</summary>
    public int VideoMaxQueuedBytes { get; set; } = 512 * 1024;

    /// <summary>Queued feedback, keyframe and NACK requests per receiver; the oldest is dropped. Default: 64.</summary>
    public int FeedbackMaxQueuedFrames { get; set; } = 64;

    /// <summary>
    /// Minimum interval between relay keyframe requests for one receiver and stream. It doubles, up to
    /// eight times, while that receiver stays congested, so a slow phone cannot make the sender emit a
    /// keyframe every second for everyone. Default: 1000 ms.
    /// </summary>
    public int KeyframeRequestIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Fraction of the video budget (queued bytes or age, whichever is fuller) at which a receiver stops getting a
    /// stream's top temporal layer. Twice this sheds every enhancement layer; half of it restores them at the next
    /// base-layer picture. Only senders that mark temporal layers are affected. Default: 0.2 (300 ms of 1500 ms).
    /// </summary>
    public double VideoLayerShedFraction { get; set; } = 0.2;
}

internal enum BoltMediaLane : byte { Feedback, Audio, Video }

/// <summary>
/// One receiver's view of one stream, for the relay's congestion reports. Counters are cumulative; the reporter
/// takes differences between its own reports.
/// </summary>
/// <param name="QueueDelayMs">Age of the oldest audio or video queued towards this receiver.</param>
/// <param name="DeliveryKbps">Rate this receiver drained at while backlogged; 0 when it has not been backlogged recently.</param>
/// <param name="TotalOfferedBytes">Every audio and video byte ever offered to this receiver.</param>
/// <param name="StreamOfferedBytes">Bytes of this stream ever offered to this receiver.</param>
/// <param name="StreamDroppedPictures">Pictures of this stream dropped for this receiver.</param>
/// <param name="StreamBaseLosses">Times this receiver lost the stream's base layer and had to wait for a keyframe.</param>
/// <param name="DroppedAudioFrames">Audio frames dropped for this receiver, all streams.</param>
/// <param name="LayerLimit">Highest temporal layer of this stream the receiver currently gets.</param>
internal readonly record struct BoltMediaQueueSnapshot(
    int QueueDelayMs,
    int DeliveryKbps,
    long TotalOfferedBytes,
    long StreamOfferedBytes,
    long StreamDroppedPictures,
    long StreamBaseLosses,
    long DroppedAudioFrames,
    int LayerLimit);

/// <summary>Outcome of offering one media frame to a receiver.</summary>
internal readonly record struct BoltMediaEnqueueResult(bool Queued, bool RequestKeyframe)
{
    public static readonly BoltMediaEnqueueResult Accepted = new(true, false);
    public static readonly BoltMediaEnqueueResult Dropped = new(false, false);
}

/// <summary>
/// Bounded, prioritized media queue for one receiving connection.
///
/// Three lanes are served strictly in order: feedback (tiny control), audio, then video. Nothing
/// here ever blocks the caller, so a sender's receive loop and every other receiver keep going
/// while one receiver's link stalls.
///
/// Video is dropped as whole pictures. Without temporal layers every delta references the picture
/// before it, so after a dropped picture the stream is dropped until the next keyframe: a decoder fed
/// a picture after a gap only shows corruption. A receiver that starts or resumes mid-stream therefore
/// waits for a keyframe, and the result tells the relay to ask the sender for one.
///
/// A sender that encodes temporal layers (L1T2/L1T3) marks each fragment with its picture's layer. A
/// receiver whose queue fills first loses the top layer, then every enhancement layer, and only after
/// that the base layer. Dropping a layer-L picture withholds every picture of layer L or above until the
/// next base-layer picture, and layers only come back at a base-layer picture: a layer-L picture refers
/// only to pictures of lower layers since the last base picture (or, for the base layer, to earlier base
/// pictures), so everything forwarded stays decodable without a keyframe.
///
/// Only clear header fields are used - sequence number, timestamp, the keyframe flag the sender puts on
/// a keyframe's first fragment and the temporal layer bits - so SFrame ciphertext, its AAD and its replay
/// checks are untouched: frames are forwarded byte for byte or not at all. A forged layer can only make
/// the relay drop more or less, which it could do anyway.
/// </summary>
internal sealed class BoltMediaSendQueue
{
    internal readonly struct Item(byte[] buffer, int length, long enqueuedAt, Guid streamId, byte layer = 0, uint picture = 0)
    {
        public byte[] Buffer { get; } = buffer;
        public int Length { get; } = length;
        public long EnqueuedAt { get; } = enqueuedAt;
        public Guid StreamId { get; } = streamId;
        /// <summary>Temporal layer of the picture this video fragment belongs to.</summary>
        public byte Layer { get; } = layer;
        /// <summary>Picture key (the clear media timestamp) of a video fragment.</summary>
        public uint Picture { get; } = picture;
        public ReadOnlyMemory<byte> Memory => Buffer.AsMemory(0, Length);
    }

    private sealed class StreamState
    {
        public bool HasSequence;
        public uint LastSequence;
        public bool AwaitingKeyframe;
        public long NextKeyframeRequestAt;
        public int KeyframeBackoffMs;
        public long LastCongestionAt = long.MinValue / 2;

        // The picture whose fragments are arriving, and what was decided for it.
        public bool InPicture;
        public uint Picture;
        public int PictureLayer;
        public bool PictureDropped;
        /// <summary>Highest temporal layer forwarded until the next base-layer picture.</summary>
        public int LayerLimit = MaxLayer;

        public long OfferedBytes;
        public long DroppedPictures;
        public long BaseLosses;
    }

    private const int MaxTrackedStreams = 64;
    private const int MaxLayer = 3;
    /// <summary>Delivery measured while backlogged is only a capacity for this long afterwards.</summary>
    private const int DeliveryFreshMs = 2_000;
    private const int DeliveryWindowMs = 200;

    private readonly BoltMediaSendQueueOptions _options;
    private readonly Func<long> _clock;
    private readonly object _sync = new();
    private readonly Queue<Item> _feedback = new();
    private readonly Queue<Item> _audio = new();
    private readonly LinkedList<Item> _video = new();
    private readonly Dictionary<Guid, StreamState> _streams = new();
    private long _audioBytes;
    private long _videoBytes;
    private bool _closed;

    private long _droppedAudio, _droppedVideo, _droppedFeedback, _staleFrames, _videoPurges, _keyframeRequests, _layerDrops;
    private long _offeredBytes;

    // Delivery measurement: the time from one dequeue to the next, while more was waiting, is the time the
    // link took to take the earlier item.
    private bool _backlogged;
    private long _lastDequeueAt;
    private int _lastDequeuedBytes;
    private long _busyMs, _busyBytes;
    private double _deliveryKbps;
    private long _lastBusyAt = long.MinValue / 2;

    public BoltMediaSendQueue(BoltMediaSendQueueOptions options, Func<long>? clock = null)
    {
        _options = options;
        _clock = clock ?? (static () => Environment.TickCount64);
    }

    public long DroppedAudioFrames => Interlocked.Read(ref _droppedAudio);
    public long DroppedVideoFrames => Interlocked.Read(ref _droppedVideo);
    public long DroppedFeedbackFrames => Interlocked.Read(ref _droppedFeedback);
    public long StaleFrames => Interlocked.Read(ref _staleFrames);
    public long VideoPurges => Interlocked.Read(ref _videoPurges);
    public long KeyframeRequests => Interlocked.Read(ref _keyframeRequests);
    /// <summary>Enhancement-layer pictures dropped so the base layer could keep flowing.</summary>
    public long LayerDrops => Interlocked.Read(ref _layerDrops);

    public long QueuedBytes { get { lock (_sync) return _audioBytes + _videoBytes + _feedback.Sum(static x => (long)x.Length); } }
    public long QueuedVideoBytes { get { lock (_sync) return _videoBytes; } }
    public long QueuedAudioBytes { get { lock (_sync) return _audioBytes; } }

    /// <summary>
    /// Offer one frame. <paramref name="keyStart"/> is the clear keyframe flag, which the sender sets on
    /// the first fragment of a keyframe. <paramref name="pictureAware"/> is false for frames without
    /// picture structure (FEC), which are dropped whenever video would be. <paramref name="picture"/> is
    /// the clear media timestamp every fragment of one picture shares; without it each frame is its own
    /// picture. <paramref name="layer"/> is the clear temporal layer.
    /// </summary>
    public BoltMediaEnqueueResult TryEnqueue(
        ReadOnlySpan<byte> frame,
        BoltMediaLane lane,
        Guid streamId,
        uint sequence,
        bool keyStart,
        bool pictureAware = true,
        uint? picture = null,
        int layer = 0)
    {
        lock (_sync)
        {
            if (_closed)
                return BoltMediaEnqueueResult.Dropped;
            var now = _clock();
            switch (lane)
            {
                case BoltMediaLane.Feedback:
                    while (_feedback.Count >= Math.Max(1, _options.FeedbackMaxQueuedFrames))
                    {
                        Release(_feedback.Dequeue());
                        DroppedFeedback();
                    }
                    _feedback.Enqueue(Copy(frame, now, streamId));
                    return BoltMediaEnqueueResult.Accepted;

                case BoltMediaLane.Audio:
                {
                    var state = State(streamId, awaitKeyframe: false);
                    if (Order(state, sequence) == SequenceOrder.Stale)
                    {
                        _staleFrames++;
                        return BoltMediaEnqueueResult.Dropped;
                    }

                    Remember(state, sequence);
                    if (frame.Length > _options.AudioMaxQueuedBytes)
                    {
                        DroppedAudio();
                        return BoltMediaEnqueueResult.Dropped;
                    }

                    // The newest audio always wins: drop what is too old or does not fit.
                    while (_audio.Count > 0 &&
                           (_audioBytes + frame.Length > _options.AudioMaxQueuedBytes ||
                            now - _audio.Peek().EnqueuedAt > _options.AudioMaxQueueDelayMs))
                    {
                        var dropped = _audio.Dequeue();
                        _audioBytes -= dropped.Length;
                        Release(dropped);
                        DroppedAudio();
                    }

                    _audio.Enqueue(Copy(frame, now, streamId));
                    _audioBytes += frame.Length;
                    _offeredBytes += frame.Length;
                    state.OfferedBytes += frame.Length;
                    return BoltMediaEnqueueResult.Accepted;
                }

                default:
                    return EnqueueVideo(frame, streamId, sequence, keyStart, pictureAware, now, picture, Math.Clamp(layer, 0, MaxLayer));
            }
        }
    }

    private BoltMediaEnqueueResult EnqueueVideo(
        ReadOnlySpan<byte> frame,
        Guid streamId,
        uint sequence,
        bool keyStart,
        bool pictureAware,
        long now,
        uint? picture,
        int layer)
    {
        // A receiver that joins or resumes mid-stream has no reference picture yet.
        var state = State(streamId, awaitKeyframe: true);
        switch (Order(state, sequence))
        {
            case SequenceOrder.Stale:
                // Retransmissions of pictures this receiver was never sent, or already moved past.
                _staleFrames++;
                return BoltMediaEnqueueResult.Dropped;
            case SequenceOrder.Restart:
                // A sender that restarted its sequence restarts its pictures too.
                PurgeVideo(streamId);
                state.AwaitingKeyframe = true;
                break;
        }

        Remember(state, sequence);
        _offeredBytes += frame.Length;
        state.OfferedBytes += frame.Length;
        if (!pictureAware)
        {
            if (state.AwaitingKeyframe || IsVideoCongested(now, frame.Length))
            {
                DroppedVideo();
                return BoltMediaEnqueueResult.Dropped;
            }

            Append(frame, now, streamId, 0, 0);
            return BoltMediaEnqueueResult.Accepted;
        }

        // Every fragment of a picture shares its decision: half a picture is never forwarded on purpose.
        var starts = keyStart || picture is null || !state.InPicture || picture.Value != state.Picture;
        if (starts)
        {
            state.InPicture = true;
            state.Picture = picture ?? 0;
            state.PictureLayer = layer;
            state.PictureDropped = false;
            if (state.AwaitingKeyframe && !keyStart)
            {
                DropPicture(state);
                return new(false, ShouldRequestKeyframe(state, now));
            }

            if (keyStart)
                state.LayerLimit = MaxLayer;
            else
                AdjustLayerLimit(state, layer, now);
            if (layer > state.LayerLimit)
            {
                DropPicture(state);
                _layerDrops++;
                return BoltMediaEnqueueResult.Dropped;
            }
        }
        else if (state.PictureDropped)
        {
            DroppedVideo();
            return state.AwaitingKeyframe ? new(false, ShouldRequestKeyframe(state, now)) : BoltMediaEnqueueResult.Dropped;
        }

        if (IsVideoCongested(now, frame.Length))
        {
            // Enhancement pictures go first: no base picture refers to them.
            if (PurgeEnhancement(state, streamId) > 0 || state.PictureLayer > 0)
                state.LayerLimit = 0;
            if (state.PictureLayer > 0)
            {
                state.PictureDropped = true;
                state.DroppedPictures++;
                _layerDrops++;
                DroppedVideo();
                return BoltMediaEnqueueResult.Dropped;
            }

            if (IsVideoCongested(now, frame.Length))
            {
                // Older pictures of this stream are now useless: the next one sent must be a keyframe.
                PurgeVideo(streamId);
                if (!keyStart || IsVideoCongested(now, frame.Length))
                {
                    state.AwaitingKeyframe = true;
                    state.BaseLosses++;
                    NoteCongestion(state, now);
                    DropPicture(state);
                    return new(false, ShouldRequestKeyframe(state, now));
                }
                // A fresh keyframe that fits once stale pictures are gone skips the receiver ahead.
            }
        }

        if (keyStart)
            state.AwaitingKeyframe = false;
        Append(frame, now, streamId, (byte)layer, state.Picture);
        return BoltMediaEnqueueResult.Accepted;
    }

    /// <summary>The rest of the arriving picture is discarded along with this fragment.</summary>
    private void DropPicture(StreamState state)
    {
        state.PictureDropped = true;
        state.DroppedPictures++;
        DroppedVideo();
    }

    /// <summary>
    /// Shed enhancement layers as this receiver's video queue fills, and restore them only at a base-layer
    /// picture, which refers to nothing a shed layer could have carried.
    /// </summary>
    private void AdjustLayerLimit(StreamState state, int layer, long now)
    {
        var shed = Math.Max(0.01, _options.VideoLayerShedFraction);
        var pressure = VideoPressure(now);
        if (pressure >= shed * 2)
            state.LayerLimit = Math.Min(state.LayerLimit, 0);
        else if (pressure >= shed)
            state.LayerLimit = Math.Min(state.LayerLimit, 1);
        else if (layer == 0 && pressure < shed / 2)
            state.LayerLimit = MaxLayer;
    }

    private double VideoPressure(long now)
    {
        var bytes = (double)_videoBytes / Math.Max(1, _options.VideoMaxQueuedBytes);
        var age = _video.First is { } oldest ? (double)(now - oldest.Value.EnqueuedAt) / Math.Max(1, _options.VideoMaxQueueDelayMs) : 0;
        return Math.Max(bytes, age);
    }

    /// <summary>Drop this stream's queued enhancement-layer fragments. Returns how many pictures went.</summary>
    private int PurgeEnhancement(StreamState state, Guid streamId)
    {
        var pictures = 0;
        uint? last = null;
        var node = _video.First;
        while (node is not null)
        {
            var next = node.Next;
            if (node.Value.StreamId == streamId && node.Value.Layer > 0)
            {
                if (last != node.Value.Picture) { pictures++; last = node.Value.Picture; }
                _videoBytes -= node.Value.Length;
                Release(node.Value);
                _video.Remove(node);
                DroppedVideo();
            }
            node = next;
        }
        state.DroppedPictures += pictures;
        _layerDrops += pictures;
        return pictures;
    }

    /// <summary>
    /// What this receiver's queue says about one stream right now. The delivery rate is only reported while
    /// it means something: measured recently, while media was actually waiting for the link.
    /// </summary>
    public BoltMediaQueueSnapshot Snapshot(Guid streamId)
    {
        lock (_sync)
        {
            var now = _clock();
            var delay = 0L;
            if (_audio.Count > 0) delay = now - _audio.Peek().EnqueuedAt;
            if (_video.First is { } oldest) delay = Math.Max(delay, now - oldest.Value.EnqueuedAt);
            _streams.TryGetValue(streamId, out var state);
            var delivery = now - _lastBusyAt <= DeliveryFreshMs ? (int)Math.Round(_deliveryKbps) : 0;
            return new BoltMediaQueueSnapshot(
                (int)Math.Clamp(delay, 0, int.MaxValue),
                delivery,
                _offeredBytes,
                state?.OfferedBytes ?? 0,
                state?.DroppedPictures ?? 0,
                state?.BaseLosses ?? 0,
                _droppedAudio,
                state?.LayerLimit ?? MaxLayer);
        }
    }

    /// <summary>Next frame to send: feedback, then audio, then video. Audio that aged out while the link stalled is dropped here too.</summary>
    public bool TryDequeue(out Item item)
    {
        lock (_sync)
        {
            var now = _clock();
            if (_feedback.TryDequeue(out item))
                return Dequeued(item, now);

            while (_audio.TryDequeue(out item))
            {
                _audioBytes -= item.Length;
                if (now - item.EnqueuedAt <= _options.AudioMaxQueueDelayMs)
                    return Dequeued(item, now);
                Release(item);
                DroppedAudio();
            }

            if (_video.First is { } first)
            {
                item = first.Value;
                _video.RemoveFirst();
                _videoBytes -= item.Length;
                return Dequeued(item, now);
            }

            _backlogged = false;
            item = default;
            return false;
        }
    }

    private bool Dequeued(Item item, long now)
    {
        if (_backlogged)
        {
            _busyMs += Math.Max(0, now - _lastDequeueAt);
            _busyBytes += _lastDequeuedBytes;
            if (_busyMs >= DeliveryWindowMs)
            {
                var kbps = _busyBytes * 8.0 / _busyMs;
                _deliveryKbps = now - _lastBusyAt > DeliveryFreshMs ? kbps : _deliveryKbps * 0.6 + kbps * 0.4;
                _lastBusyAt = now;
                _busyMs = _busyBytes = 0;
            }
        }
        _lastDequeueAt = now;
        _lastDequeuedBytes = item.Length;
        _backlogged = _feedback.Count + _audio.Count + _video.Count > 0;
        return true;
    }

    public bool IsEmpty
    {
        get
        {
            lock (_sync)
                return _feedback.Count == 0 && _audio.Count == 0 && _video.Count == 0;
        }
    }

    public static void Release(Item item)
    {
        if (item.Buffer is { Length: > 0 } buffer)
            ArrayPool<byte>.Shared.Return(buffer);
    }

    /// <summary>Forget a stream that ended, releasing any of its queued frames.</summary>
    public void ForgetStream(Guid streamId)
    {
        lock (_sync)
        {
            PurgeVideo(streamId);
            _streams.Remove(streamId);
        }
    }

    /// <summary>Release every queued buffer and refuse further frames.</summary>
    public void Close()
    {
        lock (_sync)
        {
            _closed = true;
            while (_feedback.TryDequeue(out var item)) Release(item);
            while (_audio.TryDequeue(out var item)) Release(item);
            foreach (var item in _video) Release(item);
            _video.Clear();
            _audioBytes = _videoBytes = 0;
            _streams.Clear();
        }
    }

    // Every drop is counted here and exported as bolt.server.media.relay_drops{lane}.
    private void DroppedAudio() { _droppedAudio++; BoltServerMetrics.RecordMediaRelayDrop("audio"); }
    private void DroppedVideo() { _droppedVideo++; BoltServerMetrics.RecordMediaRelayDrop("video"); }
    private void DroppedFeedback() { _droppedFeedback++; BoltServerMetrics.RecordMediaRelayDrop("feedback"); }

    private StreamState State(Guid streamId, bool awaitKeyframe)
    {
        if (_streams.TryGetValue(streamId, out var state))
            return state;
        if (_streams.Count >= MaxTrackedStreams)
            _streams.Clear();
        state = new StreamState { AwaitingKeyframe = awaitKeyframe, KeyframeBackoffMs = BaseKeyframeInterval };
        _streams[streamId] = state;
        return state;
    }

    private int BaseKeyframeInterval => Math.Max(1, _options.KeyframeRequestIntervalMs);

    private enum SequenceOrder { Fresh, Stale, Restart }

    /// <summary>Retransmission buffers hold a few hundred frames; anything further back is a restart.</summary>
    private const uint StaleWindow = 1024;

    // Sequence numbers are per stream. Over one TCP connection they only go backwards for a
    // retransmission (NACK), which for a relay-dropped frame is always too late to be useful.
    private static SequenceOrder Order(StreamState state, uint sequence)
    {
        if (!state.HasSequence)
            return SequenceOrder.Fresh;
        var behind = unchecked(state.LastSequence - sequence);
        if (behind < StaleWindow)
            return SequenceOrder.Stale;
        return unchecked(sequence - state.LastSequence) < 0x8000_0000u ? SequenceOrder.Fresh : SequenceOrder.Restart;
    }

    private static void Remember(StreamState state, uint sequence)
    {
        state.HasSequence = true;
        state.LastSequence = sequence;
    }

    private bool IsVideoCongested(long now, int incoming) =>
        _videoBytes + incoming > _options.VideoMaxQueuedBytes ||
        (_video.First is { } oldest && now - oldest.Value.EnqueuedAt > _options.VideoMaxQueueDelayMs);

    private void NoteCongestion(StreamState state, long now)
    {
        _videoPurges++;
        // Persisting congestion backs keyframe requests off; a calm spell resets them.
        state.KeyframeBackoffMs = now - state.LastCongestionAt < 10_000
            ? Math.Min(state.KeyframeBackoffMs * 2, BaseKeyframeInterval * 8)
            : BaseKeyframeInterval;
        state.LastCongestionAt = now;
    }

    private bool ShouldRequestKeyframe(StreamState state, long now)
    {
        if (now < state.NextKeyframeRequestAt)
            return false;
        state.NextKeyframeRequestAt = now + state.KeyframeBackoffMs;
        _keyframeRequests++;
        return true;
    }

    private void PurgeVideo(Guid streamId)
    {
        var node = _video.First;
        while (node is not null)
        {
            var next = node.Next;
            if (node.Value.StreamId == streamId)
            {
                _videoBytes -= node.Value.Length;
                Release(node.Value);
                _video.Remove(node);
                DroppedVideo();
            }
            node = next;
        }
    }

    private void Append(ReadOnlySpan<byte> frame, long now, Guid streamId, byte layer, uint picture)
    {
        _video.AddLast(Copy(frame, now, streamId, layer, picture));
        _videoBytes += frame.Length;
    }

    private static Item Copy(ReadOnlySpan<byte> frame, long now, Guid streamId, byte layer = 0, uint picture = 0)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(Math.Max(1, frame.Length));
        frame.CopyTo(buffer);
        return new Item(buffer, frame.Length, now, streamId, layer, picture);
    }
}
