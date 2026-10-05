using System.Collections.Concurrent;
using Bolt.Client;
using Bolt.Media.Congestion;
using Bolt.Protocol;
using Microsoft.AspNetCore.Components;

namespace Bolt.Media.Browser;

/// <summary>
/// High-level voice/video call API for Blazor WASM.
/// Wires BoltMediaClient + browser pipelines + encryption into a single service.
///
/// Usage:
///   @inject BoltMediaService Media
///   await Media.InitializeAsync(boltClient);
///   var callId = await Media.StartCallAsync("recipient", video: true);
///   await Media.EndCallAsync(callId);
/// </summary>
public sealed partial class BoltMediaService : IAsyncDisposable
{
    private readonly BoltCryptoInterop _crypto;
    private readonly BoltAudioPipeline _audio;
    private readonly BoltVideoPipeline _video;
    private readonly BoltDeviceManager _devices;
    private readonly MediaServiceOptions _options;
    private readonly ILogger<BoltMediaService> _logger;
    private readonly BoltSFrameInterop? _sframe;
    private readonly BoltRtcInterop? _rtc;
    private bool _datagramSupported;
    private string? _sframeLocalSenderId;

    private BoltMediaClient? _mediaClient;
    private sealed class PlaybackLoop
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public Task Completion { get; set; } = Task.CompletedTask;
    }

    private readonly ConcurrentDictionary<Guid, PlaybackLoop> _streamPlaybackTasks = new();

    private Guid _activeAudioStreamId;
    private Guid _activeVideoStreamId;
    private bool _hasVideo;
    private bool _initialized;
    private readonly HashSet<Guid> _configuredCalls = [];

    // Send path: one pacer and one rate loop per call, shared by the audio and video streams.
    private readonly SendPathSignals _signals = new();
    private MediaSendPacer? _pacer;
    private SendRateLoop? _rateLoop;
    private CancellationTokenSource? _rateCts;
    private Task _rateTask = Task.CompletedTask;
    /// <summary>The rate controller of a transport lost to an outage, until the resumed transport starts its own from it.</summary>
    private SendRateController? _resumeFrom;

    /// <summary>Opus packet length the encoder uses now (see <see cref="AudioPacketization"/>).</summary>
    private int _audioFrameMs = AudioPacketization.ShortFrameMs;

    /// <summary>The send estimate and its split, as the last rate-loop tick decided it.</summary>
    public SendRateDecision? SendRate { get; private set; }

    /// <summary>The last rate-loop tick in full: what the pacer, the relay, the receivers and transport feedback said (diagnostics).</summary>
    public SendRateTick? LastSendTick { get; private set; }

    /// <summary>Pictures this sender's pacer dropped, and times it lost a base-layer picture, since the call started (diagnostics).</summary>
    public long PacerDroppedPictures { get; private set; }
    /// <summary>Frames the pacer sent on the socket because the data channel would not take them (diagnostics).</summary>
    public long SocketFallbackFrames { get; private set; }
    public long PacerBaseLosses { get; private set; }

    // ── Events for Blazor UI ──

    /// <summary>Incoming call. UI should show accept/reject prompt.</summary>
    public event Func<IncomingCallInfo, Task>? OnIncomingCall;

    /// <summary>Call was answered (by remote peer or local user).</summary>
    public event Func<Guid, Task>? OnCallAnswered;

    /// <summary>Call was rejected by remote peer.</summary>
    public event Func<Guid, string?, Task>? OnCallRejected;

    /// <summary>Call ended.</summary>
    public event Func<Guid, Task>? OnCallEnded;

    public bool IsInitialized => _initialized;

    /// <summary>Get the device manager for enumeration and permissions.</summary>
    public BoltDeviceManager Devices => _devices;

    /// <summary>Run before creating or accepting an invitation; does not request microphone access.</summary>
    public Task<VoiceCapabilities> CheckVoiceCapabilitiesAsync() => _audio.CheckCapabilitiesAsync();
    public Task<AudioOutputs> GetAudioOutputsAsync() => _audio.GetAudioOutputsAsync();
    public Task<string> GetPlaybackStateAsync() => _audio.GetPlaybackStateAsync();
    public Task<bool> ResumePlaybackAsync() => _audio.ResumePlaybackAsync();
    public Task<AudioOutputs> SetAudioOutputAsync(string deviceId) => _audio.SetAudioOutputAsync(deviceId);

    /// <summary>Invoke from Start/Accept before network requests to request the microphone
    /// and resume browser audio. Encoding waits until an answered call has a stream.</summary>
    public async Task PrepareVoiceAsync()
    {
        await _audio.InitializeAsync(_options.AudioSampleRate, _options.AudioChannels, _options.AudioBitrateKbps, OpusSettings);
        await _audio.StartCaptureAsync(_options.AudioSampleRate, _options.AudioChannels, transmit: false);
    }

    /// <summary>Release prepared devices if an invitation fails or is cancelled before connection.</summary>
    public Task CancelPreparedVoiceAsync() => StopPipelinesAsync();

    public BoltMediaService(
        BoltCryptoInterop crypto,
        BoltAudioPipeline audio,
        BoltVideoPipeline video,
        BoltDeviceManager devices,
        MediaServiceOptions options,
        ILogger<BoltMediaService> logger,
        BoltSFrameInterop? sframe = null,
        BoltRtcInterop? rtc = null)
    {
        _crypto = crypto;
        _audio = audio;
        _video = video;
        _devices = devices;
        _options = options;
        _logger = logger;
        _sframe = sframe;
        _rtc = rtc;
    }

    /// <summary>
    /// Initialize the media service before connecting BoltClient or starting signaling.
    /// Loads audio modules, verifies the security mode, and installs media frame handlers
    /// so an immediately arriving call cannot be missed after registration.
    /// </summary>
    public async Task InitializeAsync(BoltClient client)
    {
        if (_initialized) return;

        if (_options.SecurityMode is not (MediaSecurityMode.AuthenticatedTransport or MediaSecurityMode.AuthenticatedSFrame))
            throw new NotSupportedException(
                "Encrypted Bolt Media calls are disabled until key exchange is bound to authenticated peer identities.");

        if (client.ServerUri.Scheme != "wss")
            throw new InvalidOperationException("Authenticated transport media requires a WSS endpoint.");

        // Initialize audio pipeline
        await _audio.InitializeAsync(_options.AudioSampleRate, _options.AudioChannels, _options.AudioBitrateKbps, OpusSettings);
        _datagramSupported = _options.DatagramTransport && _options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame &&
                             _rtc is not null && await _rtc.IsSupportedAsync();

        _mediaClient = CreateMediaClient(client);
        // The browser's view of the network, for this device's downlink reports before any probe (a slow connection).
        _networkHint = await _audio.NetworkHintAsync();

        _initialized = true;
        _logger.LogInformation("BoltMediaService initialized");
    }

    /// <summary>
    /// Wrap <paramref name="client"/> and wire every event. Used for the first transport and again for
    /// each resumed one (<see cref="AttachTransport"/>), so both are wired identically.
    /// </summary>
    private BoltMediaClient CreateMediaClient(BoltClient client)
    {
        var mediaClient = new BoltMediaClient(client, _logger)
        {
            // A hosted SFrame call outlives its socket: the host resumes it on a new connection and
            // decides itself when the call is over. Tearing down keys and capture here would make
            // every network blip a hang-up.
            EndCallsOnDisconnect = _options.SecurityMode != MediaSecurityMode.AuthenticatedSFrame
        };
        if (_options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame)
        {
            if (_sframe is null) throw new InvalidOperationException("SFrame browser services are not registered.");
            mediaClient.AuthenticatedStreamEncryptionFactory = _sframe.ForStream;
        }

        mediaClient.OnIncomingCall += async info =>
        {
            if (OnIncomingCall is not null) await OnIncomingCall(info);
        };
        mediaClient.OnCallAnswered += HandleCallAnsweredAsync;
        mediaClient.OnCallRejected += async (callId, reason) =>
        {
            await StopPipelinesAsync();
            if (OnCallRejected is not null) await OnCallRejected(callId, reason);
        };
        mediaClient.OnCallEnded += async callId =>
        {
            await StopPipelinesAsync();
            if (OnCallEnded is not null) await OnCallEnded(callId);
        };
        mediaClient.OnKeyframeRequested += streamId => { _ = _video.RequestKeyframeAsync(); };
        mediaClient.OnNackDeclined += (streamId, sequences) => { _ = DeclineVideoAsync(streamId, sequences); };
        mediaClient.OnMediaStreamConfigured += stream => { RegisterRemoteVideo(stream); StartPlaybackLoop(stream); };
        mediaClient.OnCongestionReport += report =>
            _signals.OnCongestionReport(report, report.StreamId == _activeVideoStreamId, Environment.TickCount64);
        mediaClient.OnReceiverFeedback += feedback =>
            _signals.OnReceiverFeedback(feedback, feedback.StreamId == _activeVideoStreamId, Environment.TickCount64);
        mediaClient.OnHeartbeat += (_, stamp) => OnHeartbeatEcho?.Invoke(stamp);
        // Remote senders start their pictures under this device's downlink (its start probe).
        mediaClient.DownlinkReport = DownlinkForReports;
        _transport = CreateDatagramTransport(client, mediaClient);
        return mediaClient;
    }

    private OpusEncoderSettings OpusSettings =>
        new(_options.AudioInbandFec, _options.AudioPacketLossPercent, _options.AudioDtx);

    // ── Call API ──

    /// <summary>Start a voice or voice+video call to a recipient.</summary>
    public async Task<Guid> StartCallAsync(string recipientId, bool video = false, Guid? callId = null)
    {
        EnsureInitialized();

        if (_options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame)
            throw new InvalidOperationException("Use the authenticated hosted-call and epoch APIs for SFrame.");
        _hasVideo = video;

        _audio.OnEncoded -= OnAudioEncodedForStream;
        _audio.OnEncoded += OnAudioEncodedForStream;

        var startedCallId = await _mediaClient!.StartCallAsync(recipientId, video, encrypted: false, authorizedCallId: callId);
        _logger.LogInformation("Call started: {CallId} to {Recipient}, video={Video}", startedCallId, recipientId, video);
        return startedCallId;
    }

    /// <summary>Answer an incoming call.</summary>
    public async Task AnswerCallAsync(Guid callId, bool video = false)
    {
        EnsureInitialized();
        if (_options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame)
            throw new InvalidOperationException("Use the authenticated hosted-call and epoch APIs for SFrame.");

        _hasVideo = video;

        _audio.OnEncoded -= OnAudioEncodedForStream;
        _audio.OnEncoded += OnAudioEncodedForStream;

        await _mediaClient!.AnswerCallAsync(callId, encrypted: false);
        await HandleCallAnsweredAsync(callId);
    }

    /// <summary>Reject an incoming call.</summary>
    public async Task RejectCallAsync(Guid callId)
    {
        EnsureInitialized();
        await StopPipelinesAsync();
        await _mediaClient!.RejectCallAsync(callId);
    }

    /// <summary>End an active call.</summary>
    public async Task EndCallAsync(Guid callId)
    {
        EnsureInitialized();
        await StopPipelinesAsync();
        await _mediaClient!.EndCallAsync(callId);
    }

    /// <summary>Start capturing and sending audio. Call after the call is answered.</summary>
    public async Task StartAudioAsync()
    {
        await _audio.StartCaptureAsync(_options.AudioSampleRate, _options.AudioChannels);
    }

    /// <summary>Mute an active call while retaining its microphone permission and stream.</summary>
    public Task SetAudioMutedAsync(bool muted) => _audio.SetMutedAsync(muted);

    /// <summary>Release the microphone at call end or cancellation.</summary>
    public async Task StopAudioAsync() => await _audio.StopCaptureAsync();

    // ── Internal Wiring ──

    private async Task HandleCallAnsweredAsync(Guid callId)
    {
        // An answer may be observed locally and echoed by the relay. Configure once.
        lock (_configuredCalls)
            if (!_configuredCalls.Add(callId)) return;

        var client = _mediaClient!.Client;
        var conn = client.GetPrimaryConnection();
        var writer = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();

        _activeAudioStreamId = Guid.NewGuid();
        var audioStream = new BoltMediaStream(conn, _activeAudioStreamId, callId, true);
        if (_options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame)
            audioStream.SetEncryption(_sframe!.ForStream(callId, _sframeLocalSenderId!));
        else if (_options.EnableFec) audioStream.EnableFec(_options.FecAudioGroupSize);
        // No-op over the WebSocket path: TCP already retransmits, and a gap there is a deliberate drop.
        audioStream.EnableNack(128);
        // Audio is already Opus-encoded. PCM VAD/PLC must never inspect these bytes. Its rate follows the
        // call's rate loop, not per-receiver loss hints: over TCP "loss" is a relay's deliberate drop.
        StartSendPath();
        audioStream.SetPacer(_pacer);
        if (!_mediaClient.RegisterMediaStream(audioStream))
        {
            await audioStream.DisposeAsync();
            throw new InvalidOperationException("Unable to register the local audio stream.");
        }

        BoltCodec.WriteMediaConfig(writer, _activeAudioStreamId, callId, MediaType.Audio, CodecId.Opus,
            _options.AudioSampleRate, _options.AudioChannels, _options.AudioBitrateKbps,
            _options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame ? (byte)0x10 : (byte)0, ReadOnlySpan<byte>.Empty);
        await conn.SendAsync(writer.WrittenMemory, CancellationToken.None);
        writer.Reset();

        // The camera is never opened by answering. A video stream is published only when the user
        // turns the camera on, from StartVideoAsync.
        if (_hasVideo) await StartVideoStreamAsync(callId);

        if (OnCallAnswered is not null) await OnCallAnswered(callId);
    }

    private async Task OnAudioEncodedForStream(byte[] data, uint timestamp)
    {
        if (_options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame && _sframe?.IsReady != true) return;
        var stream = _mediaClient?.GetMediaStream(_activeAudioStreamId);
        if (stream is not null)
        {
            // The capture clock is the frame timestamp: receivers size their jitter buffer against it.
            try { await stream.SendFrameAsync(data, false, captureTimestamp: timestamp); }
            catch (InvalidOperationException) when (_options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame)
            { /* A paused epoch or full bounded crypto queue drops audio, never sends plaintext. */ }
        }
    }

    private void StartPlaybackLoop(BoltMediaStream stream)
    {
        var loop = new PlaybackLoop();
        if (!_streamPlaybackTasks.TryAdd(stream.StreamId, loop))
        {
            loop.Cancellation.Dispose();
            return;
        }

        loop.Completion = Task.Run(async () =>
        {
            try
            {
                await foreach (var frame in stream.ReadFramesAsync(loop.Cancellation.Token))
                {
                    if (stream.IsAudio)
                        await _audio.DecodeFrameAsync(stream.StreamId, frame.Data, frame.Timestamp);
                    else
                        await PlayVideoFragmentAsync(stream, frame);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Playback loop error for stream {StreamId}", stream.StreamId);
            }
            finally
            {
                try
                {
                    if (stream.IsAudio) await _audio.ReleaseRemoteStreamAsync(stream.StreamId);
                    else await ReleaseRemoteVideoAsync(stream.StreamId);
                }
                finally
                {
                    if (_streamPlaybackTasks.TryRemove(stream.StreamId, out var removed))
                        removed.Cancellation.Dispose();
                }
            }
        });
    }

    // ── Send path ──

    /// <summary>Opus on the wire: the codec rate plus about 130 bytes of framing per 20 ms packet.</summary>
    private int AudioWireKbps => _options.AudioBitrateKbps + 52;

    /// <summary>
    /// Start the call's pacer and rate loop: audio before video on the way out, the transport below kept short,
    /// and the estimate driven by queue delay and the relay's and receivers' reports.
    /// </summary>
    private void StartSendPath()
    {
        if (_pacer is not null || _mediaClient is not { } media) return;
        // The pacer is bound to this transport's client: a resumed call gets a new pacer on its new client.
        var client = media.Client;
        // The pacer is bound to this transport's datagram path too: frames go to the data channel while it is
        // open and fit one message, to the socket otherwise. Both backlogs count, so the pacer keeps both short.
        var datagram = _transport;
        _signals.Clear();
        var pacer = _pacer = new MediaSendPacer(
            (frame, audio, ct) =>
            {
                if (datagram?.TrySend(frame.Span, audio) == true) return ValueTask.CompletedTask;
                if (datagram is not null) SocketFallbackFrames++;
                return client.GetPrimaryConnection().SendAsync(frame, ct);
            },
            () =>
            {
                try { return client.GetPrimaryConnection().PendingBytes + _audio.TransportBufferedBytes() + (datagram?.BufferedAmount ?? 0); }
                catch (InvalidOperationException) { return 0; }
            });
        // The pacer dropped a base picture itself: every receiver is stalled until the next keyframe.
        pacer.KeyframeNeeded += () => _ = _video.RequestKeyframeAsync(force: true);
        pacer.Start();
        var audio = _options.AudioBitrateKbps;
        var startTierKbps = VideoAdaptation.Ladder[Math.Clamp(_options.VideoStartTier, 0, VideoAdaptation.Ladder.Length - 1)].BitrateKbps;
        SendRateController controller;
        var ladder = _adaptation?.Rates ?? new VideoRateLadder();
        if (_resumeFrom is { } previous)
        {
            // A resume: the new path starts from half the old path's stable rate (never below the call's start
            // tier), with fresh delay, queue and capacity state, and the picture placed to fit before the first
            // keyframe goes out, not at the old peak.
            _resumeFrom = null;
            controller = SendRateController.Resume(previous, AudioWireKbps);
            ladder.Restart(controller.EstimateKbps - AudioWireKbps);
            pacer.ExpectKeyframe();
        }
        else
            controller = new SendRateController(
                (_adaptation?.Current?.BitrateKbps ?? startTierKbps) + AudioWireKbps,
                new SendRateOptions
                {
                    AudioNormalKbps = audio,
                    AudioLowKbps = Math.Min(24, audio),
                    AudioHighKbps = _options.AdaptiveAudioBitrate ? Math.Max(40, audio) : audio,
                    RestartFloorKbps = startTierKbps + AudioWireKbps,
                    MaxTotalKbps = Math.Max(startTierKbps + AudioWireKbps, _options.MaxSendKbps),
                });
        var loop = _rateLoop = new SendRateLoop(pacer, controller, ladder, _signals);
        // Longer Opus packets only if every receiver plays them; the encoder may already use some from the last path.
        loop.Audio.MaxFrameMs = CallMediaFormat.MaxAudioFrameMs(PeerMediaFormat);
        loop.Audio.Applied(_audioFrameMs);
        var cts = _rateCts = new CancellationTokenSource();
        _rateTask = RateLoopAsync(loop, cts.Token);
    }

    private async Task RateLoopAsync(SendRateLoop loop, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(Math.Max(100, _options.AdaptationIntervalMs), ct);
                // The start probe's padding fills the channel's buffer and the relay's feedback on purpose: no decision on that.
                if (_transport?.Probing == true) continue;
                var tick = loop.Tick(Environment.TickCount64, _encodeBacklog);
                SendRate = tick.Decision;
                LastSendTick = tick;
                PacerDroppedPictures += tick.Pacer.DroppedPictures;
                PacerBaseLosses += tick.Pacer.BaseLosses;
                if (tick.AudioKbps is { } audio && _options.AdaptiveAudioBitrate)
                    await _audio.ReconfigureBitrateAsync(_options.AudioSampleRate, _options.AudioChannels, audio);
                if (tick.AudioFrameMs is { } frameMs)
                {
                    _audioFrameMs = await _audio.SetFrameDurationAsync(frameMs);
                    loop.Audio.Applied(_audioFrameMs);
                }
                await ApplyVideoAsync(tick);
            }
        }
        catch (OperationCanceledException) { }
        catch (JSException ex) { _logger.LogDebug(ex, "Send rate loop ended with the page"); }
        catch (Exception ex) { _logger.LogWarning(ex, "Send rate loop ended"); }
    }

    /// <summary>A picture that ran this long settled somewhere worth starting the next call on this kind of network from.</summary>
    private const int RememberAfterMs = 15_000;

    private async Task RememberSettledRateAsync()
    {
        if (_rateLoop is not { } loop || _videoStartedAt is not { } started || Environment.TickCount64 - started < RememberAfterMs) return;
        _videoStartedAt = null;
        await _audio.RememberNetworkRateAsync(loop.Controller.StableKbps);
    }

    private async Task StopSendPathAsync()
    {
        var cts = _rateCts;
        _rateCts = null;
        if (cts is not null)
        {
            try { await cts.CancelAsync(); } catch (ObjectDisposedException) { }
            try { await _rateTask.WaitAsync(TimeSpan.FromSeconds(2)); } catch { /* A wedged tick must not hold up hangup. */ }
            cts.Dispose();
        }
        _rateTask = Task.CompletedTask;
        _rateLoop = null;
        SendRate = null;
        LastSendTick = null;
        if (_pacer is { } pacer)
        {
            _pacer = null;
            await pacer.DisposeAsync();
        }
        _signals.Clear();
    }

    private async Task StopPipelinesAsync()
    {
        if (_sframe is not null)
        {
            try { await _sframe.PauseAsync(); }
            catch (JSException) { /* Managed readiness is already false; still release microphone and playback. */ }
        }
        var loops = _streamPlaybackTasks.Values.ToArray();
        foreach (var loop in loops)
        {
            try { await loop.Cancellation.CancelAsync(); }
            catch (ObjectDisposedException) { /* The completed loop has already released its token. */ }
        }
        await Task.WhenAll(loops.Select(loop => loop.Completion));
        await RememberSettledRateAsync();
        await StopSendPathAsync();
        _resumeFrom = null;
        _activeAudioStreamId = Guid.Empty;
        _activeVideoStreamId = Guid.Empty;
        _hasVideo = false;
        lock (_configuredCalls) _configuredCalls.Clear();

        if (_audio.IsCapturing) await _audio.StopCaptureAsync();
        // The next call starts at 20 ms; its own link decides again.
        if (_audioFrameMs != AudioPacketization.ShortFrameMs)
        {
            try { _audioFrameMs = await _audio.SetFrameDurationAsync(AudioPacketization.ShortFrameMs); }
            catch (JSException) { _audioFrameMs = AudioPacketization.ShortFrameMs; }
        }
        await _audio.StopPlaybackAsync();
        await StopVideoPipelineAsync();
        if (_sframe is not null)
        {
            try { await _sframe.EndCallAsync(); }
            catch (JSException) { /* Browser context may already have ended; managed call keys are detached. */ }
        }
        _sframeLocalSenderId = null;
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("Call InitializeAsync(boltClient) first");
    }

    public async ValueTask DisposeAsync()
    {
        await StopPipelinesAsync();
        _audio.OnEncoded -= OnAudioEncodedForStream;
        DetachVideoHandlers();
        // The data channel (and its TURN allocation) goes with the call, not with the page.
        if (_transport is { } datagram)
        {
            _transport = null;
            try { await datagram.DisposeAsync(); }
            catch (Exception ex) { _logger.LogDebug(ex, "Closing the datagram path failed"); }
        }
        if (_mediaClient is not null) await _mediaClient.DisposeAsync();
        // These dependencies belong to the DI scope; it disposes each once after this service.
        _initialized = false;
    }
}
