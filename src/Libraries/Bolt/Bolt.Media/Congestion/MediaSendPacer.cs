using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Bolt.Tests")]

namespace Bolt.Media.Congestion;

public sealed class MediaSendPacerOptions
{
    /// <summary>Audio waiting longer than this is dropped, oldest first: late speech is worse than a gap.</summary>
    public int AudioMaxDelayMs { get; init; } = 500;
    public int AudioMaxQueued { get; init; } = 64;
    /// <summary>Video budget. Beyond it the queue drops whole pictures: enhancement layers first, then until a keyframe.</summary>
    public int VideoMaxDelayMs { get; init; } = 500;
    public int VideoMaxQueuedBytes { get; init; } = 256 * 1024;
    /// <summary>Fraction of the video budget at which the top temporal layer is shed; twice it sheds every enhancement layer.</summary>
    public double LayerShedFraction { get; init; } = 0.3;
    /// <summary>
    /// The transport below the pacer (the connection's queue and the browser's WebSocket buffer) is kept at about
    /// this much sending time, within the byte bounds. Nothing there can be prioritized or dropped.
    /// </summary>
    public int BacklogTargetMs { get; init; } = 60;
    public int MinBacklogBytes { get; init; } = 6 * 1024;
    public int MaxBacklogBytes { get; init; } = 48 * 1024;
    public int PollMs { get; init; } = 5;
    /// <summary>Fewest milliseconds between two keyframe requests this pacer raises.</summary>
    public int KeyframeRequestGapMs { get; init; } = 500;
}

/// <summary>One encoded picture, already encrypted and framed: all its fragments go out, or none of them.</summary>
/// <param name="Frames">Complete MediaFrames, in order.</param>
/// <param name="Keyframe">The picture is a keyframe.</param>
/// <param name="Layer">Temporal layer (0 = base, or a stream without layers).</param>
public sealed record PacedPicture(IReadOnlyList<byte[]> Frames, bool Keyframe, int Layer = 0);

/// <summary>What the pacer saw since the previous <see cref="MediaSendPacer.Sample"/>.</summary>
/// <param name="QueueDelayMs">Oldest waiting media plus the transport backlog at the current rate.</param>
/// <param name="SentKbps">Handed to the transport, audio and video.</param>
/// <param name="AudioKbps">The audio part of <paramref name="SentKbps"/>.</param>
/// <param name="CapacityKbps">What the transport drained while the pacer was waiting on it; 0 when the link was not the limit.</param>
/// <param name="DroppedPictures">Pictures the pacer dropped.</param>
/// <param name="BaseLosses">Times the pacer lost a base-layer picture and began waiting for a keyframe.</param>
/// <param name="DroppedAudio">Audio frames the pacer dropped.</param>
/// <param name="BacklogBytes">Transport backlog now.</param>
public readonly record struct MediaSendPacerSample(
    int QueueDelayMs,
    int SentKbps,
    int AudioKbps,
    int CapacityKbps,
    int DroppedPictures,
    int DroppedAudio,
    int BacklogBytes,
    int BaseLosses = 0);

/// <summary>
/// The sender's side of media priority. Audio and video wait in separate queues above the transport, audio is
/// always sent first, and the transport below is only fed while its backlog is short, so a burst of video
/// fragments can never put hundreds of milliseconds between a voice packet and the wire.
///
/// Video is queued and dropped as whole pictures: a picture that has started goes out completely. When the queue
/// is over budget it sheds enhancement-layer pictures first (nothing at the base layer refers to them), and
/// only then drops base pictures, after which it takes nothing but a keyframe and asks the encoder for one.
/// After shedding a layer-L picture no picture of layer L or above is taken until the next base picture.
/// </summary>
public sealed class MediaSendPacer : IAsyncDisposable
{
    private const int MaxLayer = 3;

    private readonly Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> _send;
    private readonly Func<long> _backlog;
    private readonly Func<long> _clock;
    private readonly MediaSendPacerOptions _options;
    private readonly object _sync = new();
    private readonly Queue<(byte[] Frame, long At)> _audio = new();
    private readonly LinkedList<(PacedPicture Picture, long At, int Bytes)> _video = new();
    private readonly SemaphoreSlim _work = new(0);
    private readonly CancellationTokenSource _stop = new();
    private Task _pump = Task.CompletedTask;
    private int _signalled;

    private PacedPicture? _current;
    private long _currentAt;
    private int _currentIndex;
    private int _videoBytes;
    private bool _awaitingKeyframe;
    private int _layerLimit = MaxLayer;
    private long _lastKeyframeRequest = long.MinValue / 2;

    // Window counters, reset by Sample().
    private long _sampleAt;
    private long _sentBytes, _sentAudioBytes, _blockedMs;
    private long _totalSent;
    private long _deliveredAtSample;
    private int _droppedPictures, _droppedAudio, _baseLosses;
    private int _rateKbps = 256;
    private int _capacityKbps;

    public MediaSendPacer(
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> send,
        Func<long>? transportBacklogBytes = null,
        MediaSendPacerOptions? options = null,
        Func<long>? clock = null)
    {
        _send = send;
        _backlog = transportBacklogBytes ?? (static () => 0);
        _options = options ?? new MediaSendPacerOptions();
        _clock = clock ?? (static () => Environment.TickCount64);
        _sampleAt = _clock();
    }

    /// <summary>Raised when the pacer dropped a base-layer picture and the stream needs a keyframe to recover.</summary>
    public event Action? KeyframeNeeded;

    /// <summary>The current send rate (from the rate controller), which sizes the transport backlog allowance.</summary>
    public int RateKbps { get => Volatile.Read(ref _rateKbps); set => Volatile.Write(ref _rateKbps, Math.Max(16, value)); }

    /// <summary>Transport backlog the pacer allows before it holds media back.</summary>
    public int BacklogLimitBytes => Math.Clamp(RateKbps * _options.BacklogTargetMs / 8, _options.MinBacklogBytes, _options.MaxBacklogBytes);

    public bool IsAwaitingKeyframe { get { lock (_sync) return _awaitingKeyframe; } }

    public void Start()
    {
        if (!_pump.IsCompleted) return;
        _pump = Task.Run(PumpAsync);
    }

    /// <summary>Queue one audio frame. The oldest frames go when the queue is full.</summary>
    public void EnqueueAudio(byte[] frame)
    {
        lock (_sync)
        {
            while (_audio.Count >= Math.Max(1, _options.AudioMaxQueued)) { _audio.Dequeue(); _droppedAudio++; }
            _audio.Enqueue((frame, _clock()));
        }
        Signal();
    }

    /// <summary>
    /// Would a picture with these properties be queued right now? Lets the caller skip encrypting a picture that
    /// would only be dropped (the pacer is waiting for a keyframe, or that layer is being shed).
    /// </summary>
    public bool WouldAccept(bool keyframe, int layer)
    {
        lock (_sync)
            return keyframe || (!_awaitingKeyframe && layer <= _layerLimit) || (layer == 0 && !_awaitingKeyframe);
    }

    /// <summary>Queue one picture. Returns false when it (or the stream until a keyframe) was dropped.</summary>
    public bool EnqueueVideo(PacedPicture picture)
    {
        bool accepted, requestKeyframe = false;
        lock (_sync)
        {
            accepted = Admit(picture, _clock(), ref requestKeyframe);
        }
        if (requestKeyframe) KeyframeNeeded?.Invoke();
        if (accepted) Signal();
        return accepted;
    }

    private bool Admit(PacedPicture picture, long now, ref bool requestKeyframe)
    {
        var layer = Math.Clamp(picture.Layer, 0, MaxLayer);
        var bytes = 0;
        foreach (var frame in picture.Frames) bytes += frame.Length;
        if (picture.Frames.Count == 0) return false;

        if (_awaitingKeyframe && !picture.Keyframe)
        {
            _droppedPictures++;
            requestKeyframe = RequestKeyframe(now);
            return false;
        }

        var shed = Math.Max(0.01, _options.LayerShedFraction);
        var pressure = Pressure(now, bytes);
        if (picture.Keyframe) { _awaitingKeyframe = false; _layerLimit = MaxLayer; }
        else if (pressure >= shed * 2) _layerLimit = 0;
        else if (pressure >= shed) _layerLimit = Math.Min(_layerLimit, 1);
        else if (layer == 0 && pressure < shed / 2) _layerLimit = MaxLayer;
        if (layer > _layerLimit)
        {
            _droppedPictures++;
            return false;
        }

        if (Pressure(now, bytes) > 1)
        {
            // Enhancement pictures go first: no base picture refers to them.
            if (DropQueuedEnhancement() > 0) _layerLimit = 0;
            if (layer > 0 && Pressure(now, bytes) > 1)
            {
                _layerLimit = Math.Min(_layerLimit, layer - 1);
                _droppedPictures++;
                return false;
            }
            if (Pressure(now, bytes) > 1)
            {
                // Base pictures lost: whatever is queued is now undecodable, and so is everything until a keyframe.
                _droppedPictures += _video.Count;
                _video.Clear();
                _videoBytes = 0;
                if (!picture.Keyframe || Pressure(now, bytes) > 1)
                {
                    _awaitingKeyframe = true;
                    _baseLosses++;
                    _droppedPictures++;
                    requestKeyframe = RequestKeyframe(now);
                    return false;
                }
            }
        }

        _video.AddLast((picture, now, bytes));
        _videoBytes += bytes;
        return true;
    }

    /// <summary>Fullness of the video budget (1 = full) counting the incoming picture.</summary>
    private double Pressure(long now, int incoming)
    {
        var bytes = (double)(_videoBytes + incoming) / Math.Max(1, _options.VideoMaxQueuedBytes);
        var oldest = _video.First is { } first ? first.Value.At
            : _current is not null && _currentIndex < _current.Frames.Count ? _currentAt : now;
        var age = (double)(now - oldest) / Math.Max(1, _options.VideoMaxDelayMs);
        return Math.Max(bytes, age);
    }

    private int DropQueuedEnhancement()
    {
        var dropped = 0;
        var node = _video.First;
        while (node is not null)
        {
            var next = node.Next;
            if (node.Value.Picture.Layer > 0)
            {
                _videoBytes -= node.Value.Bytes;
                _video.Remove(node);
                dropped++;
            }
            node = next;
        }
        _droppedPictures += dropped;
        return dropped;
    }

    private bool RequestKeyframe(long now)
    {
        if (now - _lastKeyframeRequest < _options.KeyframeRequestGapMs) return false;
        _lastKeyframeRequest = now;
        return true;
    }

    /// <summary>The next picture must be a keyframe (a camera starting or resuming). Requests none: the encoder starts on one.</summary>
    public void ExpectKeyframe()
    {
        lock (_sync) { _awaitingKeyframe = true; _layerLimit = MaxLayer; }
    }

    /// <summary>
    /// A picture was lost before it reached the pacer (for example its encryption queue was full). A base
    /// picture stalls the stream until a keyframe, which is requested; an enhancement picture withholds its
    /// layer and those above it until the next base picture.
    /// </summary>
    public void NotePictureLost(int layer)
    {
        bool request;
        lock (_sync)
        {
            _droppedPictures++;
            if (layer > 0) { _layerLimit = Math.Min(_layerLimit, layer - 1); return; }
            if (!_awaitingKeyframe) _baseLosses++;
            _awaitingKeyframe = true;
            request = RequestKeyframe(_clock());
        }
        if (request) KeyframeNeeded?.Invoke();
    }

    /// <summary>Forget queued video, for example when the camera stops. The picture on the wire still finishes.</summary>
    public void ClearVideo()
    {
        lock (_sync)
        {
            _video.Clear();
            _videoBytes = 0;
            _awaitingKeyframe = false;
            _layerLimit = MaxLayer;
        }
    }

    /// <summary>Next frame to send: audio first, then the rest of the picture on the wire, then the next picture.</summary>
    internal bool TryTake(out byte[] frame, out bool audio)
    {
        lock (_sync)
        {
            var now = _clock();
            while (_audio.TryDequeue(out var queued))
            {
                if (now - queued.At > _options.AudioMaxDelayMs) { _droppedAudio++; continue; }
                frame = queued.Frame;
                audio = true;
                return true;
            }
            audio = false;
            if (_current is null || _currentIndex >= _current.Frames.Count)
            {
                _current = null;
                if (_video.First is not { } next) { frame = []; return false; }
                _video.RemoveFirst();
                _videoBytes -= next.Value.Bytes;
                _current = next.Value.Picture;
                _currentAt = next.Value.At;
                _currentIndex = 0;
            }
            frame = _current.Frames[_currentIndex++];
            return true;
        }
    }

    private bool HasWork
    {
        get
        {
            lock (_sync)
                return _audio.Count > 0 || _video.Count > 0 || (_current is not null && _currentIndex < _current.Frames.Count);
        }
    }

    private void Signal()
    {
        if (Interlocked.Exchange(ref _signalled, 1) == 0) _work.Release();
    }

    private async Task PumpAsync()
    {
        var ct = _stop.Token;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (!HasWork)
                {
                    Volatile.Write(ref _signalled, 0);
                    if (!HasWork) await _work.WaitAsync(ct);
                    continue;
                }
                // Hold media above a full transport: only here can audio still overtake video.
                if (_backlog() > BacklogLimitBytes)
                {
                    var waitStarted = _clock();
                    await Task.Delay(_options.PollMs, ct);
                    lock (_sync) _blockedMs += _clock() - waitStarted;
                    continue;
                }
                if (!TryTake(out var frame, out var audio)) continue;
                try { await _send(frame, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch { lock (_sync) { if (audio) _droppedAudio++; } continue; }
                lock (_sync)
                {
                    _sentBytes += frame.Length;
                    _totalSent += frame.Length;
                    if (audio) _sentAudioBytes += frame.Length;
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Close the observation window and report on it.</summary>
    public MediaSendPacerSample Sample()
    {
        var backlog = Math.Max(0, _backlog());
        lock (_sync)
        {
            var now = _clock();
            var elapsed = Math.Max(1, now - _sampleAt);
            var sentKbps = (int)(_sentBytes * 8 / elapsed);
            var audioKbps = (int)(_sentAudioBytes * 8 / elapsed);
            // What left the transport: handed to it, minus what it still holds.
            var delivered = Math.Max(0, _totalSent - backlog);
            var drained = Math.Max(0, delivered - _deliveredAtSample);
            _deliveredAtSample = delivered;
            var linkLimited = _blockedMs * 2 >= elapsed;
            if (linkLimited) _capacityKbps = (int)(drained * 8 / elapsed);
            var capacity = linkLimited ? _capacityKbps : 0;
            var rate = Math.Max(64, capacity > 0 ? capacity : Math.Max(sentKbps, RateKbps));
            var oldest = long.MaxValue;
            if (_audio.Count > 0) oldest = _audio.Peek().At;
            if (_current is not null && _currentIndex < _current.Frames.Count) oldest = Math.Min(oldest, _currentAt);
            if (_video.First is { } first) oldest = Math.Min(oldest, first.Value.At);
            var waiting = oldest == long.MaxValue ? 0 : now - oldest;
            var queueDelay = (int)Math.Clamp(waiting + backlog * 8 / rate, 0, int.MaxValue);
            var sample = new MediaSendPacerSample(queueDelay, sentKbps, audioKbps, capacity, _droppedPictures, _droppedAudio,
                (int)Math.Min(backlog, int.MaxValue), _baseLosses);
            _sampleAt = now;
            _sentBytes = _sentAudioBytes = _blockedMs = 0;
            _droppedPictures = _droppedAudio = _baseLosses = 0;
            return sample;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        try { await _pump; } catch { /* Shutdown. */ }
        _stop.Dispose();
        _work.Dispose();
    }
}
