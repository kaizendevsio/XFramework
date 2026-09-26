using System.Threading.Channels;
using Bolt.Media.Congestion;
using Bolt.Protocol;
using Microsoft.AspNetCore.Components;

namespace Bolt.Media.Browser;

/// <summary>A remote participant's camera stream, as announced by its MediaConfig.</summary>
public sealed record RemoteVideoStream(Guid StreamId, string SenderId, VideoCodec Codec);

public sealed partial class BoltMediaService
{
    private readonly Dictionary<Guid, VideoFrameAssembler> _videoAssemblers = [];
    private readonly Dictionary<Guid, long> _videoLocalDrops = [];
    private readonly Dictionary<Guid, RemoteVideoStream> _remoteVideo = [];
    private Channel<VideoFramePayload>? _videoSend;
    private int _videoDeviceCeiling = 1080;
    private Task _videoPump = Task.CompletedTask;
    private CancellationTokenSource? _videoLoop;
    private VideoAdaptation? _adaptation;
    private VideoCodec _videoCodec;
    private bool _videoHandlers;

    /// <summary>A remote camera appeared or went away; the UI should rebuild its tiles.</summary>
    public event Action? OnRemoteVideoChanged;
    /// <summary>The browser released the local camera on its own: "hidden", "ended", "denied" or "encoder".</summary>
    public event Action<string>? OnLocalVideoStopped;
    /// <summary>The send picture changed size or rate. Null means video is suspended to protect the audio.</summary>
    public event Action<VideoTier?>? OnVideoTierChanged;

    public double? MeasuredVideoFps { get; private set; }

    public async ValueTask<VideoDiagnostics?> GetVideoDiagnosticsAsync(bool enabled)
    {
        var snapshot = await _video.DiagnosticsAsync(enabled);
        return snapshot is null ? null : snapshot with { SendQueue = _videoSend?.Reader.Count ?? 0 };
    }

    public bool IsCameraOn => _video.IsCapturing;
    public VideoCodec ActiveVideoCodec => _videoCodec;
    public VideoTier? ActiveVideoTier => _adaptation?.Current;
    public IReadOnlyCollection<RemoteVideoStream> RemoteVideo { get { lock (_remoteVideo) return _remoteVideo.Values.ToArray(); } }

    /// <summary>Probe this device's encoders. Opens no camera and needs no permission.</summary>
    public Task<VideoCapabilities> CheckVideoCapabilitiesAsync() => _video.CheckCapabilitiesAsync();
    public Task<MediaDeviceInfo[]> CamerasAsync() => _video.CamerasAsync();
    public Task AttachLocalPreviewAsync(ElementReference element) => _video.AttachPreviewAsync(element);
    public Task DetachLocalPreviewAsync() => _video.DetachPreviewAsync();
    public async Task<bool> AttachRemoteVideoAsync(Guid streamId, ElementReference canvas)
    {
        RemoteVideoStream? remote;
        lock (_remoteVideo) remote = _remoteVideo.GetValueOrDefault(streamId);
        if (remote is null) return false;
        var attached = await _video.AddRemoteAsync(streamId, canvas, VideoCodecLadder.Name(remote.Codec));
        // The first picture may have arrived before Blazor mounted the canvas; a new decoder
        // needs a reference picture immediately, including after expanding a minimized call.
        if (attached && _mediaClient is { } client) await client.RequestRemoteKeyframeAsync(streamId, force: true);
        return attached;
    }

    /// <summary>
    /// Turn the camera on for an answered, key-active call.
    ///
    /// This is the only path that opens a camera. It publishes a video stream bound to the same
    /// SFrame epoch as the audio, so a picture can never leave this device in the clear.
    /// </summary>
    public async Task<VideoCaptureState> StartVideoAsync(Guid callId, VideoCodec codec, int ceilingHeight,
        string? deviceId = null, string? facingMode = null, int? preferredHeight = null, int preferredFramerate = 30)
    {
        EnsureInitialized();
        if (codec == VideoCodec.None) throw new InvalidOperationException("No video codec is shared with this call.");
        if (_options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame && !IsSFrameReady)
            throw new InvalidOperationException("The call's encryption keys are not active yet.");

        // Every call starts on a mobile-safe rung and climbs on measured headroom. The user's
        // preference is how high it may climb, never where it starts: starting at 1080p on a
        // 512 kbps link filled the relay's queue within a second.
        var startHeight = VideoAdaptation.Ladder[Math.Clamp(_options.VideoStartTier, 0, VideoAdaptation.Ladder.Length - 1)].Height;
        _videoDeviceCeiling = Math.Min(preferredHeight ?? _options.VideoMaxHeight, Math.Min(ceilingHeight, _options.VideoMaxHeight));
        var adaptation = _adaptation ??= new VideoAdaptation(
            VideoAdaptation.IndexForHeight(Math.Min(startHeight, _videoDeviceCeiling)), preferredFramerate);
        adaptation.SetCeiling(_videoDeviceCeiling);
        var tier = adaptation.Current ?? VideoAdaptation.Ladder[0];
        if (_videoCodec != codec || !_video.IsCapturing)
        {
            _videoCodec = codec;
            while (true)
            {
                try
                {
                    await _video.InitializeEncoderAsync(VideoCodecLadder.Name(codec), tier, _options.KeyframeIntervalSeconds, _options.TemporalLayers);
                    break;
                }
                catch (JSException) when (tier.Framerate > 30 || tier.Height > VideoAdaptation.Ladder[0].Height)
                {
                    // isConfigSupported at 30 fps cannot promise 60 fps on this device; a size it refused is a ceiling.
                    if (tier.Framerate > 30) adaptation.LimitTo30Fps();
                    else
                    {
                        _videoDeviceCeiling = VideoAdaptation.Ladder.Last(x => x.Height < tier.Height).Height;
                        adaptation.SetCeiling(_videoDeviceCeiling);
                    }
                    tier = adaptation.Current!.Value;
                }
            }
        }

        await StartVideoStreamAsync(callId);
        _mediaClient!.ConfigureVideoFeedback(_activeVideoStreamId, tier.BitrateKbps);
        AttachVideoHandlers();
        // The stream starts on a keyframe: the encoder forces one, and the pacer takes nothing before it.
        _pacer?.ExpectKeyframe();
        if (_rateLoop is { } loop)
        {
            loop.Ladder = adaptation.Rates;
            loop.Controller.Reset(tier.BitrateKbps + AudioWireKbps);
            adaptation.Suspended = false;
        }
        StartAdaptationLoop(); // Camera callbacks can arrive before startCapture's promise resolves.
        try { return await _video.StartCaptureAsync(deviceId, facingMode); }
        catch
        {
            StopAdaptationLoop();
            DrainVideoSend();
            await _videoPump;
            throw;
        }
    }

    public void ResetVideoPreference() => _adaptation = null;

    /// <summary>Camera off. Releases the capture device but keeps the published stream, so
    /// turning it back on costs one getUserMedia and no renegotiation.</summary>
    public async Task StopVideoAsync()
    {
        StopAdaptationLoop();
        MeasuredVideoFps = null;
        await _video.StopCaptureAsync();
        DrainVideoSend();
        await _videoPump;
    }

    /// <summary>Lower the ceiling as the call grows; every extra sender is another decode.</summary>
    public async Task SetVideoParticipantsAsync(int senders)
    {
        if (_adaptation is not { } adaptation) return;
        if (adaptation.SetCeiling(Math.Min(_videoDeviceCeiling, VideoAdaptation.HeightCapForParticipants(senders))) &&
            adaptation.Current is { } tier)
        { await _video.ApplyTierAsync(tier); OnVideoTierChanged?.Invoke(tier); }
    }

    // ── Local stream lifecycle ──

    private async Task StartVideoStreamAsync(Guid callId)
    {
        if (_activeVideoStreamId != Guid.Empty) return;
        var conn = _mediaClient!.Client.GetPrimaryConnection();
        var streamId = Guid.NewGuid();
        var stream = new BoltMediaStream(conn, streamId, callId, false);
        if (_options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame)
            stream.SetEncryption(_sframe!.ForStream(callId, _sframeLocalSenderId!));
        else if (_options.EnableFec) stream.EnableFec(_options.FecVideoGroupSize);
        // No-op over the WebSocket path: TCP already retransmits, and a gap there is a deliberate drop.
        stream.EnableNack(256);
        stream.SetPacer(_pacer);
        var tier = _adaptation?.Current ?? VideoAdaptation.Ladder[_options.VideoStartTier];
        // Receiver loss hints are not wired to the encoder: over TCP a sequence gap is a deliberate relay drop
        // (with temporal layers, a quarter to half of all pictures), and a keyframe for each would feed the
        // congestion. Decoders and the relay ask for keyframes explicitly (MediaKeyRequest).
        if (!_mediaClient.RegisterMediaStream(stream))
        {
            await stream.DisposeAsync();
            throw new InvalidOperationException("Unable to register the local video stream.");
        }
        _activeVideoStreamId = streamId;

        var writer = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteMediaConfig(writer, streamId, callId, MediaType.Video, VideoCodecLadder.ToCodecId(_videoCodec),
            tier.Width, tier.Height, tier.BitrateKbps,
            _options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame ? (byte)0x10 : (byte)0, ReadOnlySpan<byte>.Empty);
        await conn.SendAsync(writer.WrittenMemory, CancellationToken.None);
        writer.Reset();
    }

    private void AttachVideoHandlers()
    {
        if (_videoHandlers) return;
        _videoHandlers = true;
        _video.OnEncoded += QueueEncodedVideo;
        _video.OnCaptureStopped += HandleCaptureStopped;
        _video.OnDecodeFailed += HandleDecodeFailed;
    }

    private void DetachVideoHandlers()
    {
        if (!_videoHandlers) return;
        _videoHandlers = false;
        _video.OnEncoded -= QueueEncodedVideo;
        _video.OnCaptureStopped -= HandleCaptureStopped;
        _video.OnDecodeFailed -= HandleDecodeFailed;
    }

    private void HandleCaptureStopped(string reason)
    {
        StopAdaptationLoop();
        DrainVideoSend();
        OnLocalVideoStopped?.Invoke(reason);
    }

    private void HandleDecodeFailed(string streamId)
    {
        // The browser rebuilds the decoder itself; it just needs a fresh reference picture to start from.
        if (Guid.TryParse(streamId, out var id) && _mediaClient is { } client)
        {
            lock (_remoteVideo) _videoAssemblers.GetValueOrDefault(id)?.Reset();
            _ = client.RequestRemoteKeyframeAsync(id);
        }
    }

    // ── Send path: fragment, then encrypt each fragment like an audio packet ──

    private void QueueEncodedVideo(byte[] data, bool isKeyframe, uint frameId, uint timestamp, int layer)
    {
        if (_activeVideoStreamId == Guid.Empty) return;
        if (_options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame && _sframe?.IsReady != true) return;
        if (_pacer is { } pacer && !pacer.WouldAccept(isKeyframe, layer)) return;
        if (data.Length == 0 || VideoFrameFragments.FragmentCount(data.Length) > VideoFrameFragments.MaxFragments) { VideoDropped(layer); return; }
        var channel = _videoSend;
        if (channel is null) return;
        // A whole picture is queued or dropped as one: half a picture on the wire is wasted bandwidth.
        if (!channel.Writer.TryWrite(new VideoFramePayload(data, timestamp, isKeyframe, FrameId: frameId, Layer: layer))) VideoDropped(layer);
    }

    /// <summary>
    /// A picture never reached the pacer. An enhancement picture only withholds its layer until the next base
    /// picture; a base picture means nothing decodes until a keyframe, which the pacer asks the encoder for.
    /// </summary>
    private void VideoDropped(int layer) => _pacer?.NotePictureLost(layer);

    private async Task PumpVideoAsync(Channel<VideoFramePayload> channel, CancellationToken ct)
    {
        try
        {
            await foreach (var picture in channel.Reader.ReadAllAsync(ct))
            {
                var stream = _mediaClient?.GetMediaStream(_activeVideoStreamId);
                if (stream is null) continue;
                // Fragment only accepted pictures: dropped pictures allocate no fragment arrays.
                var fragments = VideoFrameFragments.Split(picture.Data, picture.FrameId, picture.TimestampMicroseconds, picture.IsKeyframe, picture.Layer);
                try
                {
                    var sent = await stream.SendPictureAsync(fragments, picture.IsKeyframe,
                        (uint)((ulong)picture.TimestampMicroseconds * 90 / 1000), picture.Layer, ct);
                    if (!sent && _pacer is null) VideoDropped(picture.Layer);
                }
                catch (InvalidOperationException) { VideoDropped(picture.Layer); } // Paused epoch: drop, never send plaintext.
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogWarning(ex, "Video send loop ended"); }
    }

    // ── Receive path ──

    private void RegisterRemoteVideo(BoltMediaStream stream)
    {
        if (stream.IsAudio) return;
        var codec = VideoCodecLadder.FromCodecId(stream.Codec);
        lock (_remoteVideo)
        {
            _remoteVideo[stream.StreamId] = new(stream.StreamId, stream.SenderId, codec);
            _videoAssemblers[stream.StreamId] = new VideoFrameAssembler();
            _videoLocalDrops[stream.StreamId] = 0;
        }
        OnRemoteVideoChanged?.Invoke();
    }

    private async Task PlayVideoFragmentAsync(BoltMediaStream stream, ReadOnlyMemory<byte> fragment)
    {
        VideoFrameAssembler? assembler;
        lock (_remoteVideo)
        {
            assembler = _videoAssemblers.GetValueOrDefault(stream.StreamId);
            // Fragments this device dropped itself were not dropped by any layer policy: the next gap is a break.
            var drops = stream.LocalDrops;
            if (assembler is not null && _videoLocalDrops.GetValueOrDefault(stream.StreamId) != drops)
            {
                _videoLocalDrops[stream.StreamId] = drops;
                assembler.MarkLocalLoss();
            }
        }
        if (assembler?.Add(fragment.Span) is not { } picture) return;
        await _video.DecodeFrameAsync(stream.StreamId, picture.Data, picture.TimestampMicroseconds, picture.IsKeyframe, picture.Discontinuity);
    }

    private async Task ReleaseRemoteVideoAsync(Guid streamId)
    {
        bool removed;
        lock (_remoteVideo) { removed = _remoteVideo.Remove(streamId); _videoAssemblers.Remove(streamId); _videoLocalDrops.Remove(streamId); }
        if (!removed) return;
        await _video.RemoveRemoteAsync(streamId);
        OnRemoteVideoChanged?.Invoke();
    }

    // ── Camera loop ──

    private void StartAdaptationLoop()
    {
        if (_videoLoop is not null) return;
        _videoSend ??= Channel.CreateBounded<VideoFramePayload>(new BoundedChannelOptions(3)
        { FullMode = BoundedChannelFullMode.Wait, SingleReader = false });
        var loop = _videoLoop = new CancellationTokenSource();
        _videoPump = PumpVideoAsync(_videoSend, loop.Token);
    }

    private void StopAdaptationLoop()
    {
        var loop = _videoLoop;
        _videoLoop = null;
        if (loop is null) return;
        try { loop.Cancel(); } catch (ObjectDisposedException) { }
        loop.Dispose();
    }

    /// <summary>
    /// Apply one rate-loop decision to the camera: suspend or resume it, or move the encoder to a new size,
    /// frame rate or bitrate. Runs from the call's rate loop; does nothing while no camera is on.
    /// </summary>
    private async Task ApplyVideoAsync(SendRateTick tick)
    {
        if (_adaptation is not { } adaptation || _videoLoop is null) return;
        if (!_video.IsCapturing && !adaptation.Suspended) return;
        var stats = await _video.StatsAsync();
        MeasuredVideoFps = stats.Fps;
        _encodeBacklog = stats.Backlog;
        if (tick.SuspendVideo)
        {
            // Suspension keeps the call alive on a link that cannot carry any picture at all.
            adaptation.Suspended = true;
            await _video.StopCaptureAsync();
            _pacer?.ClearVideo();
            OnVideoTierChanged?.Invoke(null);
            return;
        }
        if (tick.ResumeVideo && adaptation.Suspended)
        {
            adaptation.Suspended = false;
            if (adaptation.Current is { } resumed) await _video.ApplyTierAsync(resumed);
            _pacer?.ExpectKeyframe();
            await _video.StartCaptureAsync();
            OnVideoTierChanged?.Invoke(adaptation.Current);
            return;
        }
        if (tick.Video is not { } setting || adaptation.Suspended) return;
        var previous = _appliedTier;
        var tier = VideoAdaptation.ToTier(setting);
        if (await _video.ApplyTierAsync(tier))
        {
            _appliedTier = tier;
            _logger.LogDebug("Video {Tier} (estimate {Estimate} kbps, delay {Delay} ms)", tier, tick.Decision.TotalKbps, tick.Decision.DelayMs);
            if (previous is not { } before || before.Height != tier.Height || before.Framerate != tier.Framerate)
                OnVideoTierChanged?.Invoke(tier);
        }
    }

    private VideoTier? _appliedTier;
    private int _encodeBacklog;

    private void DrainVideoSend()
    {
        if (_videoSend is { } channel)
            while (channel.Reader.TryRead(out _)) { }
        _pacer?.ClearVideo();
    }

    private async Task StopVideoPipelineAsync()
    {
        StopAdaptationLoop();
        DetachVideoHandlers();
        if (_video.IsCapturing) await _video.StopCaptureAsync();
        _videoSend?.Writer.TryComplete();
        try { await _videoPump.WaitAsync(TimeSpan.FromSeconds(2)); } catch { /* A wedged pump must not hold up hangup. */ }
        _videoPump = Task.CompletedTask;
        _videoSend = null;
        Guid[] remotes;
        lock (_remoteVideo) { remotes = _remoteVideo.Keys.ToArray(); _remoteVideo.Clear(); _videoAssemblers.Clear(); _videoLocalDrops.Clear(); }
        foreach (var streamId in remotes)
        { try { await _video.RemoveRemoteAsync(streamId); } catch (JSException) { /* The page is going away. */ } }
        _adaptation = null;
        _appliedTier = null;
        MeasuredVideoFps = null;
        _videoCodec = VideoCodec.None;
        _encodeBacklog = 0;
        if (remotes.Length != 0) OnRemoteVideoChanged?.Invoke();
    }
}
