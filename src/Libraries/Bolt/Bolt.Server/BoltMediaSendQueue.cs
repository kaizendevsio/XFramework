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
    /// <summary>Queued audio older than this is dropped, oldest first. Late speech is worse than a gap. Default: 500 ms.</summary>
    public int AudioMaxQueueDelayMs { get; set; } = 500;

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
}

internal enum BoltMediaLane : byte { Feedback, Audio, Video }

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
/// Video is dropped as whole pictures and then until the next keyframe: without temporal layers
/// every delta references the picture before it, so a decoder fed a picture after a gap only
/// shows corruption. A receiver that starts or resumes mid-stream therefore waits for a keyframe,
/// and the result tells the relay to ask the sender for one.
///
/// Only clear header fields are used - sequence number, timestamp and the keyframe flag the sender
/// puts on a keyframe's first fragment - so SFrame ciphertext, its AAD and its replay checks are
/// untouched: frames are forwarded byte for byte or not at all.
/// </summary>
internal sealed class BoltMediaSendQueue
{
    internal readonly struct Item(byte[] buffer, int length, long enqueuedAt, Guid streamId)
    {
        public byte[] Buffer { get; } = buffer;
        public int Length { get; } = length;
        public long EnqueuedAt { get; } = enqueuedAt;
        public Guid StreamId { get; } = streamId;
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
    }

    private const int MaxTrackedStreams = 64;

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

    private long _droppedAudio, _droppedVideo, _droppedFeedback, _staleFrames, _videoPurges, _keyframeRequests;

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

    public long QueuedBytes { get { lock (_sync) return _audioBytes + _videoBytes + _feedback.Sum(static x => (long)x.Length); } }
    public long QueuedVideoBytes { get { lock (_sync) return _videoBytes; } }
    public long QueuedAudioBytes { get { lock (_sync) return _audioBytes; } }

    /// <summary>
    /// Offer one frame. <paramref name="keyStart"/> is the clear keyframe flag, which the sender sets on
    /// the first fragment of a keyframe. <paramref name="pictureAware"/> is false for frames without
    /// picture structure (FEC), which are dropped whenever video would be.
    /// </summary>
    public BoltMediaEnqueueResult TryEnqueue(
        ReadOnlySpan<byte> frame,
        BoltMediaLane lane,
        Guid streamId,
        uint sequence,
        bool keyStart,
        bool pictureAware = true)
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
                        _droppedFeedback++;
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
                        _droppedAudio++;
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
                        _droppedAudio++;
                    }

                    _audio.Enqueue(Copy(frame, now, streamId));
                    _audioBytes += frame.Length;
                    return BoltMediaEnqueueResult.Accepted;
                }

                default:
                    return EnqueueVideo(frame, streamId, sequence, keyStart, pictureAware, now);
            }
        }
    }

    private BoltMediaEnqueueResult EnqueueVideo(
        ReadOnlySpan<byte> frame,
        Guid streamId,
        uint sequence,
        bool keyStart,
        bool pictureAware,
        long now)
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
        if (!pictureAware)
        {
            if (state.AwaitingKeyframe || IsVideoCongested(now, frame.Length))
            {
                _droppedVideo++;
                return BoltMediaEnqueueResult.Dropped;
            }

            Append(frame, now, streamId);
            return BoltMediaEnqueueResult.Accepted;
        }

        if (state.AwaitingKeyframe && !keyStart)
        {
            _droppedVideo++;
            return new(false, ShouldRequestKeyframe(state, now));
        }

        if (IsVideoCongested(now, frame.Length))
        {
            // Older pictures of this stream are now useless: the next one sent must be a keyframe.
            PurgeVideo(streamId);
            if (!keyStart || IsVideoCongested(now, frame.Length))
            {
                state.AwaitingKeyframe = true;
                NoteCongestion(state, now);
                _droppedVideo++;
                return new(false, ShouldRequestKeyframe(state, now));
            }
            // A fresh keyframe that fits once stale pictures are gone skips the receiver ahead.
        }

        if (keyStart)
            state.AwaitingKeyframe = false;
        Append(frame, now, streamId);
        return BoltMediaEnqueueResult.Accepted;
    }

    /// <summary>Next frame to send: feedback, then audio, then video. Audio that aged out while the link stalled is dropped here too.</summary>
    public bool TryDequeue(out Item item)
    {
        lock (_sync)
        {
            if (_feedback.TryDequeue(out item))
                return true;

            var now = _clock();
            while (_audio.TryDequeue(out item))
            {
                _audioBytes -= item.Length;
                if (now - item.EnqueuedAt <= _options.AudioMaxQueueDelayMs)
                    return true;
                Release(item);
                _droppedAudio++;
            }

            if (_video.First is { } first)
            {
                item = first.Value;
                _video.RemoveFirst();
                _videoBytes -= item.Length;
                return true;
            }

            item = default;
            return false;
        }
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
                _droppedVideo++;
            }
            node = next;
        }
    }

    private void Append(ReadOnlySpan<byte> frame, long now, Guid streamId)
    {
        _video.AddLast(Copy(frame, now, streamId));
        _videoBytes += frame.Length;
    }

    private static Item Copy(ReadOnlySpan<byte> frame, long now, Guid streamId)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(Math.Max(1, frame.Length));
        frame.CopyTo(buffer);
        return new Item(buffer, frame.Length, now, streamId);
    }
}
