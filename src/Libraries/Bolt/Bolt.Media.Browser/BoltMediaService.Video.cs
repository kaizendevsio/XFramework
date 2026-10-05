using System.Threading.Channels;
using Bolt.Media.Congestion;
using Bolt.Protocol;
using Microsoft.AspNetCore.Components;

namespace Bolt.Media.Browser;

/// <summary>A remote participant's camera stream, as announced by its MediaConfig.</summary>
public sealed record RemoteVideoStream(Guid StreamId, string SenderId, VideoCodec Codec);

/// <summary>One remote camera stream's receive path, cumulative (see <see cref="BoltMediaService.GetVideoReceiveStats"/>).</summary>
public sealed record VideoReceiveStats(Guid StreamId, long Fragments, long LocalDrops, int Nacked, int Recovered, int Abandoned,
    int Declined, int Incomplete, int Skipped, long Pictures, int RecoveryMs);

public sealed partial class BoltMediaService
{
    private readonly Dictionary<Guid, VideoRecoveryBuffer> _videoAssemblers = [];
    /// <summary>One remote picture stream decodes in order: fragments, recovery polls and declines take turns.</summary>
    private readonly Dictionary<Guid, SemaphoreSlim> _videoGates = [];
    private CancellationTokenSource? _recoveryLoop;
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
        var path = MediaPath;
        return snapshot is null ? null : snapshot with
        {
            SendQueue = _videoSend?.Reader.Count ?? 0,
            // Both legs: "UDP/relay (relay UDP/relay)". A TCP or TLS leg on either side is the first thing to look for.
            Transport = path.RelayLeg is { } relayLeg ? $"{path.Description} (relay {relayLeg})" : path.Description, TransportReason = path.Reason, TransportRttMs = path.RttMs, AudioRedundancy = path.AudioRedundancy,
        };
    }

    /// <summary>
    /// What each remote camera stream's receive path did with what arrived, cumulative since it appeared: fragments that
    /// reached reassembly, fragments this device dropped itself before decrypting them (a backlog), what loss recovery
    /// asked for and got, and pictures handed to the decoder or given up.
    /// </summary>
    public IReadOnlyList<VideoReceiveStats> GetVideoReceiveStats()
    {
        lock (_remoteVideo)
            return _videoAssemblers.Select(x => new VideoReceiveStats(x.Key, x.Value.Fragments,
                _mediaClient?.GetMediaStream(x.Key)?.LocalDrops ?? 0, x.Value.Nacked, x.Value.Recovered, x.Value.Abandoned,
                x.Value.Declined, x.Value.Incomplete, x.Value.Skipped, x.Value.Pictures, x.Value.RecoveryMs)).ToArray();
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

        // A new picture starts where the link is known to carry it: this device's start probe (measured while the call
        // rang), the worst receiver's reported downlink, the last call on this network, or a middle picture, never above
        // the user's preference. Starting at 1080p on a 512 kbps link filled the relay's queue within a second; starting
        // at 240p on a fast one took half a minute to reach the preference. The start-up ramp takes it from there.
        var startHeight = VideoAdaptation.Ladder[Math.Clamp(_options.VideoStartTier, 0, VideoAdaptation.Ladder.Length - 1)].Height;
        _videoDeviceCeiling = Math.Min(preferredHeight ?? _options.VideoMaxHeight, Math.Min(ceilingHeight, _options.VideoMaxHeight));
        var fresh = _adaptation is null;
        var adaptation = _adaptation ??= new VideoAdaptation(
            VideoAdaptation.IndexForHeight(Math.Min(startHeight, _videoDeviceCeiling)), preferredFramerate);
        adaptation.SetCeiling(_videoDeviceCeiling);
        if (fresh && _rateLoop is { } starting)
        {
            var hints = await StartHintsAsync();
            starting.Ladder = adaptation.Rates;
            starting.BeginPicture(Environment.TickCount64, hints, AudioWireKbps);
            _logger.LogInformation("Video starts at {Tier}: {Start} (probe {Probe})", adaptation.Current, starting.StartedAt, LinkProbe);
        }
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
            // A camera turned on again keeps the path's history: the picture it last held, never above a known limit.
            if (!fresh) loop.Controller.Reset(tier.BitrateKbps + AudioWireKbps);
            adaptation.Suspended = false;
        }
        _videoStartedAt ??= Environment.TickCount64;
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
        // A lost fragment the relay never got (the uplink lost it) is asked of this sender; it goes again on the data
        // channel only. With no channel the request cannot even arrive: NACKs exist on datagram paths alone.
        stream.EnableRetransmission(frame => _transport?.TrySend(frame.Span) == true);
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

    private void QueueEncodedVideo(byte[] data, bool isKeyframe, uint frameId, uint timestamp, int layer, int orientation)
    {
        if (_activeVideoStreamId == Guid.Empty) return;
        if (_options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame && _sframe?.IsReady != true) return;
        if (_pacer is { } pacer && !pacer.WouldAccept(isKeyframe, layer)) return;
        if (data.Length == 0) { VideoDropped(layer); return; }
        if (data.Length > VideoFrameFragments.MaxPictureBytes)
        {
            // Too big to carry at all: the picture is too large, not the link too small. A keyframe is needed, but no
            // congestion is reported (the rate controller would cut tenfold); the picture steps down a rung instead.
            _pacer?.NotePictureTooLarge(layer);
            if (_adaptation?.Rates.ReduceForCpu() == true && _adaptation.Current is { } smaller) _ = _video.ApplyTierAsync(smaller);
            return;
        }
        var channel = _videoSend;
        if (channel is null) return;
        // A whole picture is queued or dropped as one: half a picture on the wire is wasted bandwidth.
        if (!channel.Writer.TryWrite(new VideoFramePayload(data, timestamp, isKeyframe, FrameId: frameId, Layer: layer, Orientation: orientation)))
            VideoDropped(layer);
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
                // A turned picture encoded before a member who cannot read its orientation joined: that member would
                // drop it (or show it on its side). The encoder is already switching to upright pixels on a keyframe;
                // losing this picture asks for that keyframe too.
                if (!CallMediaFormat.CanSend(PeerMediaFormat, picture.Orientation)) { VideoDropped(picture.Layer); continue; }
                // Fragment only accepted pictures: dropped pictures allocate no fragment arrays. On a datagram path
                // every fragment must fit one message after encryption; anything bigger rides the WebSocket.
                var fragments = VideoFrameFragments.Split(picture.Data, picture.FrameId, picture.TimestampMicroseconds, picture.IsKeyframe,
                    picture.Layer, VideoFragmentPayload(stream, picture.Data.Length), picture.Orientation);
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

    /// <summary>
    /// Fragment payload for the current path. Over the WebSocket, the SFrame bound (4 KB). Over a data channel,
    /// whatever leaves one message for the Bolt header and the SFrame ciphertext overhead this stream's
    /// encryption actually adds, plus room for its context to grow; a picture that would need more fragments
    /// than the header can count goes out at the WebSocket size (and so on the WebSocket). The SFrame
    /// operation itself is unchanged: smaller plaintexts, same keys, same AAD, same replay checks.
    /// </summary>
    private int VideoFragmentPayload(BoltMediaStream stream, int pictureBytes)
    {
        var message = _transport?.MaxMessageBytes ?? 0;
        if (message <= 0) return VideoFrameFragments.MaxPayload;
        var overhead = BoltCodec.MediaFrameHeaderSize + (stream.EncryptionOverhead > 0 ? stream.EncryptionOverhead : VideoFrameFragments.DefaultEncryptionOverhead) +
                       VideoFrameFragments.ContextSlack + VideoFrameFragments.HeaderSize;
        var payload = message - overhead;
        if (payload < VideoFrameFragments.MinPayload || VideoFrameFragments.FragmentCount(pictureBytes, payload) > VideoFrameFragments.MaxFragments)
            return VideoFrameFragments.MaxPayload;
        return payload;
    }

    // ── Receive path ──

    private void RegisterRemoteVideo(BoltMediaStream stream)
    {
        if (stream.IsAudio) return;
        var codec = VideoCodecLadder.FromCodecId(stream.Codec);
        lock (_remoteVideo)
        {
            _remoteVideo[stream.StreamId] = new(stream.StreamId, stream.SenderId, codec);
            _videoAssemblers[stream.StreamId] = new VideoRecoveryBuffer();
            _videoGates[stream.StreamId] = new SemaphoreSlim(1, 1);
            _videoLocalDrops[stream.StreamId] = 0;
            if (_recoveryLoop is null)
            {
                var loop = _recoveryLoop = new CancellationTokenSource();
                _ = RecoverVideoAsync(loop.Token);
            }
        }
        OnRemoteVideoChanged?.Invoke();
    }

    private Task PlayVideoFragmentAsync(BoltMediaStream stream, MediaFrameData frame) =>
        StepVideoAsync(stream.StreamId, (buffer, ready, _) =>
        {
            // Fragments this device dropped itself were not dropped by any layer policy: the next gap is a break.
            var drops = stream.LocalDrops;
            if (_videoLocalDrops.GetValueOrDefault(stream.StreamId) != drops)
            {
                _videoLocalDrops[stream.StreamId] = drops;
                buffer.MarkLocalLoss();
            }
            buffer.Push(frame.SequenceNumber, frame.Data.Span, Environment.TickCount64, ready);
        });

    /// <summary>
    /// Every 20 ms while remote video plays: ask the relay again for what is still missing and release pictures whose
    /// wait is over (see <see cref="VideoRecoveryBuffer"/>). Idle on a WebSocket path, where recovery is off.
    /// </summary>
    private async Task RecoverVideoAsync(CancellationToken ct)
    {
        try
        {
            var wait = 20;
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(wait, ct);
                Guid[] streams;
                lock (_remoteVideo) streams = _videoAssemblers.Where(x => x.Value.RecoveryMs > 0).Select(x => x.Key).ToArray();
                // Nothing recovering (a WebSocket path): look again four times a second instead of fifty.
                wait = streams.Length == 0 ? 250 : 20;
                foreach (var streamId in streams)
                    await StepVideoAsync(streamId, (buffer, ready, nacks) => buffer.Poll(Environment.TickCount64, ready, nacks));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogDebug(ex, "Video recovery loop ended"); }
    }

    private Task DeclineVideoAsync(Guid streamId, uint[] sequences) =>
        StepVideoAsync(streamId, (buffer, ready, _) => buffer.Decline(sequences, ready));

    /// <summary>
    /// One step of a remote stream's reassembly, under that stream's turn: recovery follows the path (on with a data
    /// channel whose relay resends, sized by its round trip), NACKs go out on the channel, and released pictures are
    /// decoded in order.
    /// </summary>
    private async Task StepVideoAsync(Guid streamId, Action<VideoRecoveryBuffer, List<VideoFramePayload>, List<uint>> step)
    {
        SemaphoreSlim? gate;
        lock (_remoteVideo) gate = _videoGates.GetValueOrDefault(streamId);
        if (gate is null) return;
        try { await gate.WaitAsync(); }
        catch (ObjectDisposedException) { return; }
        try
        {
            var ready = new List<VideoFramePayload>();
            var nacks = new List<uint>();
            var transport = _transport;
            var recover = transport?.SupportsNack == true;
            var rtt = transport?.Status.RttMs is double measured && measured > 0 ? (int)Math.Round(measured) : 300;
            var switched = false;
            lock (_remoteVideo)
            {
                if (_videoAssemblers.GetValueOrDefault(streamId) is not { } buffer) return;
                switched = buffer.Configure(recover, rtt);
                step(buffer, ready, nacks);
            }
            // Recovery switched on or off: the two modes share nothing, so the decoder restarts from a keyframe.
            // Forced: the decoder waits from here, and a request coalesced away would leave it waiting for the sender's
            // safety keyframe, seconds off.
            if (switched && _mediaClient is { } client) _ = client.RequestRemoteKeyframeAsync(streamId, force: true);
            // At most 64 numbers a request: what the relay serves per request, and well inside one datagram.
            for (var offset = 0; transport is not null && offset < nacks.Count; offset += 64)
            {
                var chunk = nacks.GetRange(offset, Math.Min(64, nacks.Count - offset));
                var writer = new System.Buffers.ArrayBufferWriter<byte>(BoltCodec.NackRequestHeaderSize + chunk.Count * 4);
                BoltCodec.WriteNackRequest(writer, streamId, chunk.ToArray());
                transport.TrySend(writer.WrittenSpan);
            }
            foreach (var picture in ready)
                await _video.DecodeFrameAsync(streamId, picture.Data, picture.TimestampMicroseconds, picture.IsKeyframe, picture.Discontinuity,
                    picture.Orientation);
        }
        finally
        {
            try { gate.Release(); } catch (ObjectDisposedException) { }
        }
    }

    private async Task ReleaseRemoteVideoAsync(Guid streamId)
    {
        bool removed;
        CancellationTokenSource? idle = null;
        lock (_remoteVideo)
        {
            removed = _remoteVideo.Remove(streamId); _videoAssemblers.Remove(streamId); _videoGates.Remove(streamId); _videoLocalDrops.Remove(streamId);
            // The last remote camera went off: nothing left to recover, so the 20 ms timer stops waking the page.
            if (_remoteVideo.Count == 0) { idle = _recoveryLoop; _recoveryLoop = null; }
        }
        if (idle is not null) { try { await idle.CancelAsync(); } catch (ObjectDisposedException) { } idle.Dispose(); }
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
    /// <summary>When this call's camera first started (for remembering where the call settled).</summary>
    private long? _videoStartedAt;

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
        CancellationTokenSource? recovery;
        lock (_remoteVideo)
        {
            remotes = _remoteVideo.Keys.ToArray(); _remoteVideo.Clear(); _videoAssemblers.Clear(); _videoGates.Clear(); _videoLocalDrops.Clear();
            recovery = _recoveryLoop; _recoveryLoop = null;
        }
        if (recovery is not null) { try { await recovery.CancelAsync(); } catch (ObjectDisposedException) { } recovery.Dispose(); }
        foreach (var streamId in remotes)
        { try { await _video.RemoveRemoteAsync(streamId); } catch (JSException) { /* The page is going away. */ } }
        _adaptation = null;
        _appliedTier = null;
        _videoStartedAt = null;
        MeasuredVideoFps = null;
        _videoCodec = VideoCodec.None;
        _encodeBacklog = 0;
        if (remotes.Length != 0) OnRemoteVideoChanged?.Invoke();
    }
}
