#if HARNESS_ADAPTIVE
// The phase 1 sender: the browser client's real send path (Bolt.Media's MediaSendPacer, SendRateController,
// VideoRateLadder and SendRateLoop) driving a synthetic encoder instead of WebCodecs. Only the encoder and the
// crypto are models; queueing, priorities, drops, rate control and the relay's feedback are the real thing.
using System.Buffers;
using System.Diagnostics;
using System.Net.WebSockets;
using Bolt.Media.Congestion;
using Bolt.Protocol;

/// <summary>What the encoder model does with a budget: pictures at the rung's rate, keyframes a multiple of a delta, temporal layers.</summary>
internal sealed record AdaptiveProfile(int StartHeight, int AudioKbps, int KeyframeRatio, int KeyframeIntervalMs, bool TemporalLayers, double Overshoot);

internal sealed class AdaptiveSender(Stopwatch clock) : IAsyncDisposable
{
    private const int FragmentPayload = 4084 + 12 + 26; // fragment + fragment header + SFrame overhead
    private readonly ClientWebSocket _socket = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly Guid _audio = Guid.NewGuid(), _video = Guid.NewGuid();
    private readonly Random _random = new(1234);
    private long _inFlight;
    private uint _audioSequence, _videoSequence;
    private volatile bool _keyframeRequested, _keyframeForced;
    private Task _loops = Task.CompletedTask;
    private SendRateLoop? _loop;
    private MediaSendPacer? _pacer;
    private volatile int _audioKbps = 32;
    private volatile bool _suspended;
    private VideoSetting _setting;
    private readonly object _settingSync = new();
    private readonly List<(double At, string Rung)> _rungTimeline = [];
    private readonly List<(double At, int Estimate, int Video, int Delay, bool Suspended)> _estimates = [];
    public long AudioSent, VideoSent, Keyframes, KeyRequests, PacerDroppedPictures, Reports, FeedbackReports;
    public bool TemporalLayers { get; private set; }

    public async Task ConnectAsync(Uri uri)
    {
        await _socket.ConnectAsync(uri, CancellationToken.None);
        await SendRawAsync(Frames.Write(w => BoltCodec.WriteRegister(w, "sender", "sender")));
        var buffer = new byte[64 * 1024];
        await Frames.ReceiveAsync(_socket, buffer, CancellationToken.None); // RegisterAck
        _ = Task.Run(ReceiveLoopAsync);
    }

    public void Start(Guid call, AdaptiveProfile profile)
    {
        TemporalLayers = profile.TemporalLayers;
        var ladder = new VideoRateLadder(VideoRateLadder.IndexForHeight(profile.StartHeight));
        var start = ladder.Current.Rung;
        // The overload offer: start at the rung's full nominal rate, as a client that ignored the link would.
        ladder.Place(start.MaxKbps * 2 / 3, 0, congested: true);
        _setting = ladder.Current;
        _pacer = new MediaSendPacer((frame, _) => SendPacedAsync(frame), () => Interlocked.Read(ref _inFlight));
        _pacer.KeyframeNeeded += () => _keyframeForced = true;
        _pacer.Start();
        var controller = new SendRateController(_setting.BitrateKbps + profile.AudioKbps + 52,
            new SendRateOptions { AudioNormalKbps = profile.AudioKbps, AudioLowKbps = Math.Min(24, profile.AudioKbps) });
        _loop = new SendRateLoop(_pacer, controller, ladder);
        _audioKbps = profile.AudioKbps;
        _loops = Task.WhenAll(
            Task.Run(() => AudioLoopAsync(call)),
            Task.Run(() => VideoLoopAsync(call, profile)),
            Task.Run(RateLoopAsync));
    }

    private async ValueTask SendPacedAsync(ReadOnlyMemory<byte> frame)
    {
        Interlocked.Add(ref _inFlight, frame.Length);
        try { await SendRawAsync(frame); }
        finally { Interlocked.Add(ref _inFlight, -frame.Length); }
    }

    private async Task SendRawAsync(ReadOnlyMemory<byte> frame)
    {
        await _sendLock.WaitAsync();
        try { await _socket.SendAsync(frame, WebSocketMessageType.Binary, true, CancellationToken.None); }
        finally { _sendLock.Release(); }
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[64 * 1024];
        try
        {
            while (!_stop.IsCancellationRequested && await Frames.ReceiveAsync(_socket, buffer, _stop.Token) is { } message)
                foreach (var frame in Frames.Unbatch(message))
                {
                    var now = Environment.TickCount64;
                    switch ((FrameType)frame[0])
                    {
                        case FrameType.MediaKeyRequest when BoltCodec.TryReadMediaKeyRequest(frame, out var stream) && stream == _video:
                            Interlocked.Increment(ref KeyRequests);
                            _keyframeRequested = true;
                            break;
                        case FrameType.MediaCongestion when BoltCodec.TryReadMediaCongestion(frame, out var report):
                            Interlocked.Increment(ref Reports);
                            _loop?.Signals.OnCongestionReport(report, report.StreamId == _video, now);
                            break;
                        case FrameType.MediaFeedback when BoltCodec.TryReadMediaFeedback(frame, out var feedback):
                            if (feedback.HasDelayReport) Interlocked.Increment(ref FeedbackReports);
                            _loop?.Signals.OnReceiverFeedback(feedback, feedback.StreamId == _video, now);
                            break;
                    }
                }
        }
        catch { /* Shutdown. */ }
    }

    private async Task AudioLoopAsync(Guid call)
    {
        await SendRawAsync(Frames.Write(w => BoltCodec.WriteMediaConfig(w, _audio, call, MediaType.Audio, CodecId.Opus, 48_000, 1, _audioKbps, 0x10, [])));
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(20));
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token))
            {
                // Opus bytes for 20 ms at the current rate, plus the SFrame header and tag.
                var payload = Payload.Create(_audioKbps * 20 / 8 + 26, Payload.Audio);
                var sequence = ++_audioSequence;
                var timestamp = (uint)(clock.ElapsedMilliseconds * 48); // capture clock, 48 kHz
                _pacer!.EnqueueAudio(Frames.Write(w => BoltCodec.WriteMediaFrame(w, _audio, sequence, timestamp, MediaFrameFlags.Encrypted, payload)));
                Interlocked.Increment(ref AudioSent);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task VideoLoopAsync(Guid call, AdaptiveProfile profile)
    {
        VideoSetting setting;
        lock (_settingSync) setting = _setting;
        await SendRawAsync(Frames.Write(w => BoltCodec.WriteMediaConfig(w, _video, call, MediaType.Video, CodecId.H264,
            setting.Rung.Width, setting.Rung.Height, setting.BitrateKbps, 0x10, [])));
        uint picture = 0;
        long lastKeyframe = long.MinValue / 2;
        VideoRung? lastRung = null;
        var cycle = 0;
        uint lastBase = 0, lastLayer1 = 0;
        var next = clock.Elapsed.TotalMilliseconds;
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                lock (_settingSync) setting = _setting;
                var fps = setting.Rung.Framerate;
                next += 1000.0 / fps;
                var wait = next - clock.Elapsed.TotalMilliseconds;
                if (wait > 0) await Task.Delay(TimeSpan.FromMilliseconds(wait), _stop.Token);
                else next = clock.Elapsed.TotalMilliseconds;
                if (_suspended) { lastRung = null; continue; }

                var now = clock.ElapsedMilliseconds;
                // The browser encoder's policy: a keyframe on a new size, when the pacer lost a base picture, on a
                // coalesced request at most once a second, and on the long safety interval.
                var isKey = lastRung != setting.Rung || _keyframeForced || now - lastKeyframe >= profile.KeyframeIntervalMs ||
                            (_keyframeRequested && now - lastKeyframe >= 1000);
                lastRung = setting.Rung;
                // L1T3 from 24 fps (0,2,1,2), L1T2 from 12 fps (0,1), else none - as the browser picks.
                var mode = !profile.TemporalLayers ? 1 : fps >= 24 ? 3 : fps >= 12 ? 2 : 1;
                if (isKey) cycle = 0;
                var layer = isKey ? 0 : mode switch { 3 => new[] { 0, 2, 1, 2 }[cycle % 4], 2 => cycle % 2, _ => 0 };
                cycle++;
                if (isKey) { lastKeyframe = now; _keyframeRequested = _keyframeForced = false; Interlocked.Increment(ref Keyframes); }

                // References are the encoder's, whatever is dropped later: the receiver checks that every picture it
                // gets really can be decoded, which is what proves the drop policy right.
                picture++;
                var reference = isKey ? uint.MaxValue : layer == 0 ? lastBase : layer == 1 ? lastBase : Math.Max(lastBase, lastLayer1);
                if (layer == 0) { lastBase = picture; lastLayer1 = 0; }
                else if (layer == 1) lastLayer1 = picture;
                if (_pacer is { } gate && !gate.WouldAccept(isKey, layer)) { Interlocked.Increment(ref PacerDroppedPictures); continue; }

                // Bits per picture: the rate over the frame rate, layers sized like H.264 L1T3 (T0 50%, T1 25%, T2 25%),
                // keyframes a multiple of a delta, VBR noise, and the encoder's overshoot.
                var perPicture = setting.BitrateKbps * 1000 / 8.0 / fps * profile.Overshoot;
                var weight = mode switch { 3 => layer switch { 0 => 2.0, 1 => 1.0, _ => 0.5 }, 2 => layer == 0 ? 1.33 : 0.67, _ => 1.0 };
                var size = (int)(perPicture * (isKey ? profile.KeyframeRatio : weight) * (0.85 + _random.NextDouble() * 0.3));
                size = Math.Max(64, size);
                var count = Math.Max(1, (size + FragmentPayload - 1) / FragmentPayload);
                var timestamp = (uint)(now * 90);
                var frames = new List<byte[]>(count);
                for (var index = 0; index < count; index++)
                {
                    var payload = Payload.Create(Math.Min(FragmentPayload, size - index * FragmentPayload), Payload.Video, isKey, picture, index, count,
                        layer, reference);
                    var sequence = ++_videoSequence;
                    var flags = MediaFrameFlags.WithTemporalLayer((byte)(MediaFrameFlags.Encrypted | (isKey && index == 0 ? MediaFrameFlags.Keyframe : 0)), layer);
                    frames.Add(Frames.Write(w => BoltCodec.WriteMediaFrame(w, _video, sequence, timestamp, flags, payload)));
                }
                if (_pacer!.EnqueueVideo(new PacedPicture(frames, isKey, layer))) Interlocked.Increment(ref VideoSent);
                else Interlocked.Increment(ref PacerDroppedPictures);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task RateLoopAsync()
    {
        try
        {
            var lastLog = 0L;
            while (!_stop.IsCancellationRequested)
            {
                await Task.Delay(SendRateLoop.IntervalMs, _stop.Token);
                var tick = _loop!.Tick(Environment.TickCount64);
                if (tick.AudioKbps is { } audio) _audioKbps = audio;
                if (tick.SuspendVideo) _suspended = true;
                if (tick.ResumeVideo) _suspended = false;
                if (tick.Video is { } video)
                    lock (_settingSync)
                    {
                        if (video.Rung != _setting.Rung) _rungTimeline.Add((Math.Round(clock.Elapsed.TotalSeconds, 2), video.Rung.ToString()));
                        _setting = video;
                    }
                var seconds = Math.Round(clock.Elapsed.TotalSeconds, 2);
                VideoSetting current;
                lock (_settingSync) current = _setting;
                _estimates.Add((seconds, tick.Decision.TotalKbps, _suspended ? 0 : current.BitrateKbps, tick.Decision.DelayMs, _suspended));
                if (clock.ElapsedMilliseconds - lastLog >= 1000)
                {
                    lastLog = clock.ElapsedMilliseconds;
                    Env.Log($"RATE t={seconds:F0} estimate={tick.Decision.TotalKbps} video={(_suspended ? "suspended" : current.ToString())} " +
                            $"audio={_audioKbps}k delay={tick.Decision.DelayMs} signal={tick.Decision.Signal} sent={tick.Pacer.SentKbps} " +
                            $"localQueue={tick.Pacer.QueueDelayMs} relay={Describe(tick.Relay)} receiver={Describe(tick.Receiver)} fb={FeedbackReports} rep={Reports}");
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static string Describe(RelaySignal? relay) => relay is { } r
        ? $"q{r.QueueDelayMs}/up{r.UplinkDelayMs}/cap{r.CapacityKbps}{(r.Dropping ? "/drop" : "")}" : "-";
    private static string Describe(ReceiverSignal? receiver) => receiver is { } r ? $"q{r.QueueDelayMs}/got{r.ReceivedKbps}" : "-";

    /// <summary>How the rate control behaved, for the summary: where it settled, how fast, and whether it flapped.</summary>
    public object Report(int seconds)
    {
        lock (_settingSync)
        {
            var settledFrom = Math.Min(30, seconds / 3.0);
            var settled = _estimates.Where(x => x.At >= settledFrom).ToArray();
            // Converged: the first moment after which the video bitrate stays within 25% of its settled median.
            var median = settled.Length == 0 ? 0 : settled.Select(x => x.Video).Order().ElementAt(settled.Length / 2);
            double? converged = null;
            for (var i = _estimates.Count - 1; i >= 0; i--)
            {
                if (Math.Abs(_estimates[i].Video - median) > Math.Max(40, median * 0.25)) break;
                converged = _estimates[i].At;
            }
            return new
            {
                temporalLayers = TemporalLayers,
                finalRung = _setting.Rung.ToString(),
                finalVideoKbps = _suspended ? 0 : _setting.BitrateKbps,
                settledVideoKbpsMedian = median,
                settledEstimateKbpsMedian = settled.Length == 0 ? 0 : settled.Select(x => x.Estimate).Order().ElementAt(settled.Length / 2),
                convergedAtS = converged,
                rungChanges = _rungTimeline.Count,
                rungChangesAfterSettle = _rungTimeline.Count(x => x.At >= settledFrom),
                suspendedSeconds = Math.Round(_estimates.Count(x => x.Suspended) * SendRateLoop.IntervalMs / 1000.0, 1),
                rungTimeline = string.Join(" ", _rungTimeline.Take(40).Select(x => $"{x.At:F1}s:{x.Rung}")),
                pacerDroppedPictures = PacerDroppedPictures,
                congestionReports = Reports,
                receiverDelayReports = FeedbackReports
            };
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        try { await _loops; } catch { }
        if (_pacer is not null) await _pacer.DisposeAsync();
        _socket.Abort();
        _socket.Dispose();
    }
}

/// <summary>A receiver's delay report, per stream, as the browser client's receive-side controller sends it.</summary>
internal sealed class ReceiverFeedback(Guid stream, bool audio)
{
    private readonly MediaQueuingDelayEstimator _delay = new();
    private long _bytes, _windowStartedAt;
    private double _kbps;
    private uint _highest;

    public void Observe(uint sequence, uint timestamp, int bytes, long now)
    {
        _bytes += bytes;
        if (unchecked((int)(sequence - _highest)) > 0) _highest = sequence;
        _delay.Observe(timestamp, audio ? 48 : 90, now);
    }

    /// <summary>The report for the window since the previous one, or null when nothing arrived (a stale delay is no report).</summary>
    public byte[]? Build(long now)
    {
        if (_bytes == 0 && _windowStartedAt != 0) { _windowStartedAt = now; return null; }
        var elapsed = _windowStartedAt == 0 ? 0 : now - _windowStartedAt;
        _windowStartedAt = now;
        if (elapsed > 0)
        {
            var kbps = _bytes * 8.0 / elapsed;
            _kbps = _kbps <= 0 ? kbps : _kbps * 0.7 + kbps * 0.3;
        }
        _bytes = 0;
        var writer = new ArrayBufferWriter<byte>();
        BoltCodec.WriteMediaFeedback(writer, stream, _highest, 0, 0, 0, QualityHint.Maintain,
            (ushort)Math.Clamp(_delay.DelayMs, 0, ushort.MaxValue), (uint)Math.Max(0, Math.Round(_kbps)));
        return writer.WrittenSpan.ToArray();
    }
}
#endif
