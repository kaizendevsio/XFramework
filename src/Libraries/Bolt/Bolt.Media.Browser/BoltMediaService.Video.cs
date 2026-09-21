using System.Threading.Channels;
using Bolt.Protocol;
using Microsoft.AspNetCore.Components;

namespace Bolt.Media.Browser;

/// <summary>A remote participant's camera stream, as announced by its MediaConfig.</summary>
public sealed record RemoteVideoStream(Guid StreamId, string SenderId, VideoCodec Codec);

public sealed partial class BoltMediaService
{
    private readonly Dictionary<Guid, VideoFrameAssembler> _videoAssemblers = [];
    private readonly Dictionary<Guid, RemoteVideoStream> _remoteVideo = [];
    private Channel<VideoFramePayload>? _videoSend;
    private int _videoDeviceCeiling = 1080;
    private bool _videoNeedsKeyframe;
    private Task _videoPump = Task.CompletedTask;
    private CancellationTokenSource? _videoLoop;
    private VideoAdaptation? _adaptation;
    private VideoCodec _videoCodec;
    private int _videoAllowedKbps;
    private int _videoDropped;
    private bool _videoHandlers;

    /// <summary>A remote camera appeared or went away; the UI should rebuild its tiles.</summary>
    public event Action? OnRemoteVideoChanged;
    /// <summary>The browser released the local camera on its own: "hidden", "ended", "denied" or "encoder".</summary>
    public event Action<string>? OnLocalVideoStopped;
    /// <summary>The send ladder moved. Null means video is suspended to protect the audio.</summary>
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
        if (attached && _mediaClient is { } client) await client.RequestRemoteKeyframeAsync(streamId);
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

        var requestedHeight = preferredHeight ?? VideoAdaptation.Ladder[Math.Clamp(_options.VideoStartTier, 0, VideoAdaptation.Ladder.Length - 1)].Height;
        var adaptation = _adaptation ??= new VideoAdaptation(VideoAdaptation.IndexForHeight(requestedHeight), preferredFramerate);
        _videoDeviceCeiling = Math.Min(requestedHeight, Math.Min(ceilingHeight, _options.VideoMaxHeight));
        adaptation.SetCeiling(_videoDeviceCeiling);
        var tier = adaptation.Current ?? VideoAdaptation.Ladder[0];
        if (_videoCodec != codec || !_video.IsCapturing)
        {
            _videoCodec = codec;
            while (true)
            {
                try { await _video.InitializeEncoderAsync(VideoCodecLadder.Name(codec), tier, _options.KeyframeIntervalSeconds); break; }
                catch (JSException) when (tier.Framerate > 30 || adaptation.Index > 0)
                {
                    // isConfigSupported at 30 fps cannot promise 60 fps on this device.
                    adaptation = _adaptation = new VideoAdaptation(tier.Framerate > 30 ? adaptation.Index : adaptation.Index - 1, 30);
                    _videoDeviceCeiling = Math.Min(_videoDeviceCeiling, adaptation.Current!.Value.Height);
                    adaptation.SetCeiling(_videoDeviceCeiling);
                    tier = adaptation.Current!.Value;
                }
            }
        }

        await StartVideoStreamAsync(callId);
        _mediaClient!.ConfigureVideoFeedback(_activeVideoStreamId, tier.BitrateKbps);
        Volatile.Write(ref _videoAllowedKbps, tier.BitrateKbps);
        AttachVideoHandlers();
        _videoNeedsKeyframe = true;
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
        stream.EnableNack(256);
        var tier = _adaptation?.Current ?? VideoAdaptation.Ladder[_options.VideoStartTier];
        // The authenticated relay rejects the legacy plaintext probe packets.
        // Adapt using real receiver feedback and local encoder/queue measurements.
        // Receiver-driven congestion control is the only view of the far end this sender gets.
        stream.OnBitrateChanged += kbps => Volatile.Write(ref _videoAllowedKbps, kbps);
        stream.OnKeyframeNeeded += () => _ = _video.RequestKeyframeAsync();
        if (!_mediaClient.RegisterMediaStream(stream))
        {
            await stream.DisposeAsync();
            throw new InvalidOperationException("Unable to register the local video stream.");
        }
        _activeVideoStreamId = streamId;
        Volatile.Write(ref _videoAllowedKbps, tier.BitrateKbps);

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

    private void QueueEncodedVideo(byte[] data, bool isKeyframe, uint frameId, uint timestamp)
    {
        if (_activeVideoStreamId == Guid.Empty) return;
        if (_options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame && _sframe?.IsReady != true) return;
        if (_videoNeedsKeyframe && !isKeyframe) return;
        if (data.Length == 0 || VideoFrameFragments.FragmentCount(data.Length) > VideoFrameFragments.MaxFragments) { VideoDropped(); return; }
        var channel = _videoSend;
        if (channel is null) return;
        // A whole picture is queued or dropped as one: half a picture on the wire is wasted bandwidth.
        if (!channel.Writer.TryWrite(new VideoFramePayload(data, timestamp, isKeyframe, FrameId: frameId))) VideoDropped();
        else if (isKeyframe) _videoNeedsKeyframe = false;
    }

    private void VideoDropped()
    {
        _videoDropped++;
        if (_videoNeedsKeyframe) return;
        _videoNeedsKeyframe = true;
        _ = _video.RequestKeyframeAsync();
    }

    private async Task PumpVideoAsync(Channel<VideoFramePayload> channel, CancellationToken ct)
    {
        try
        {
            await foreach (var picture in channel.Reader.ReadAllAsync(ct))
            {
                var stream = _mediaClient?.GetMediaStream(_activeVideoStreamId);
                if (stream is null) continue;
                // Fragment only accepted pictures: dropped pictures allocate no fragment arrays.
                var fragments = VideoFrameFragments.Split(picture.Data, picture.FrameId, picture.TimestampMicroseconds, picture.IsKeyframe);
                for (var index = 0; index < fragments.Count; index++)
                {
                    try { await stream.SendFrameAsync(fragments[index], index == 0 && (fragments[0][0] & 0x01) != 0, ct, (uint)((ulong)picture.TimestampMicroseconds * 90 / 1000)); }
                    catch (InvalidOperationException) { _videoDropped++; break; } // Paused epoch: drop, never send plaintext.
                }
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
        }
        OnRemoteVideoChanged?.Invoke();
    }

    private async Task PlayVideoFragmentAsync(Guid streamId, ReadOnlyMemory<byte> fragment)
    {
        VideoFrameAssembler? assembler;
        lock (_remoteVideo) assembler = _videoAssemblers.GetValueOrDefault(streamId);
        if (assembler?.Add(fragment.Span) is not { } picture) return;
        await _video.DecodeFrameAsync(streamId, picture.Data, picture.TimestampMicroseconds, picture.IsKeyframe, picture.Discontinuity);
    }

    private async Task ReleaseRemoteVideoAsync(Guid streamId)
    {
        bool removed;
        lock (_remoteVideo) { removed = _remoteVideo.Remove(streamId); _videoAssemblers.Remove(streamId); }
        if (!removed) return;
        await _video.RemoveRemoteAsync(streamId);
        OnRemoteVideoChanged?.Invoke();
    }

    // ── Adaptation ──

    private void StartAdaptationLoop()
    {
        if (_videoLoop is not null) return;
        _videoSend ??= Channel.CreateBounded<VideoFramePayload>(new BoundedChannelOptions(3)
        { FullMode = BoundedChannelFullMode.Wait, SingleReader = false });
        var loop = _videoLoop = new CancellationTokenSource();
        _videoPump = PumpVideoAsync(_videoSend, loop.Token);
        _ = AdaptAsync(loop.Token);
    }

    private void StopAdaptationLoop()
    {
        var loop = _videoLoop;
        _videoLoop = null;
        if (loop is null) return;
        try { loop.Cancel(); } catch (ObjectDisposedException) { }
        loop.Dispose();
    }

    private async Task AdaptAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(_options.AdaptationIntervalMs, ct);
                // A suspended ladder must keep observing: it is the only thing that can resume it.
                if (_adaptation is not { } adaptation || (!_video.IsCapturing && !adaptation.Suspended)) continue;
                var stats = await _video.StatsAsync();
                MeasuredVideoFps = stats.Fps;
                var backlog = _videoSend is { } channel ? channel.Reader.Count : 0;
                var conditions = new VideoConditions(Volatile.Read(ref _videoAllowedKbps), stats.Fps, stats.Backlog, backlog + _videoDropped);
                _videoDropped = 0;
                var before = adaptation.Suspended;
                var tier = adaptation.Observe(conditions);
                if (adaptation.Suspended != before)
                {
                    // Suspension keeps the call alive on a link that cannot carry any picture at all.
                    if (adaptation.Suspended) await _video.StopCaptureAsync();
                    else await _video.StartCaptureAsync();
                    OnVideoTierChanged?.Invoke(adaptation.Current);
                    continue;
                }
                if (tier is not { } next) continue;
                if (await _video.ApplyTierAsync(next))
                {
                    _logger.LogDebug("Video tier {Tier} (allowed {Allowed} kbps, {Fps:F1} fps)", next, conditions.AllowedKbps, stats.Fps);
                    OnVideoTierChanged?.Invoke(next);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogWarning(ex, "Video adaptation loop ended"); }
    }

    private void DrainVideoSend()
    {
        if (_videoSend is not { } channel) return;
        while (channel.Reader.TryRead(out _)) { }
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
        lock (_remoteVideo) { remotes = _remoteVideo.Keys.ToArray(); _remoteVideo.Clear(); _videoAssemblers.Clear(); }
        foreach (var streamId in remotes)
        { try { await _video.RemoveRemoteAsync(streamId); } catch (JSException) { /* The page is going away. */ } }
        _adaptation = null;
        MeasuredVideoFps = null;
        _videoCodec = VideoCodec.None;
        _videoAllowedKbps = 0;
        _videoDropped = 0;
        _videoNeedsKeyframe = false;
        if (remotes.Length != 0) OnRemoteVideoChanged?.Invoke();
    }
}
