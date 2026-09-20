using System.Collections.Concurrent;
using Bolt.Client;
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
        await _audio.InitializeAsync(_options.AudioSampleRate, _options.AudioChannels, _options.AudioBitrateKbps);
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
        BoltSFrameInterop? sframe = null)
    {
        _crypto = crypto;
        _audio = audio;
        _video = video;
        _devices = devices;
        _options = options;
        _logger = logger;
        _sframe = sframe;
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
        await _audio.InitializeAsync(_options.AudioSampleRate, _options.AudioChannels, _options.AudioBitrateKbps);

        // Create media client and wire events
        _mediaClient = new BoltMediaClient(client, _logger);
        if (_options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame)
        {
            if (_sframe is null) throw new InvalidOperationException("SFrame browser services are not registered.");
            _mediaClient.AuthenticatedStreamEncryptionFactory = _sframe.ForStream;
        }

        _mediaClient.OnIncomingCall += async info =>
        {
            if (OnIncomingCall is not null) await OnIncomingCall(info);
        };
        _mediaClient.OnCallAnswered += HandleCallAnsweredAsync;
        _mediaClient.OnCallRejected += async (callId, reason) =>
        {
            await StopPipelinesAsync();
            if (OnCallRejected is not null) await OnCallRejected(callId, reason);
        };
        _mediaClient.OnCallEnded += async callId =>
        {
            await StopPipelinesAsync();
            if (OnCallEnded is not null) await OnCallEnded(callId);
        };
        _mediaClient.OnKeyframeRequested += streamId => { _ = _video.RequestKeyframeAsync(); };
        _mediaClient.OnMediaStreamConfigured += stream => { RegisterRemoteVideo(stream); StartPlaybackLoop(stream); };

        _initialized = true;
        _logger.LogInformation("BoltMediaService initialized");
    }

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
        audioStream.EnableNack(128);
        // Audio is already Opus-encoded. PCM VAD/PLC must never inspect these bytes.
        audioStream.OnBitrateChanged += kbps =>
            _ = _audio.ReconfigureBitrateAsync(_options.AudioSampleRate, _options.AudioChannels, kbps);
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

    private async Task OnAudioEncodedForStream(byte[] data)
    {
        if (_options.SecurityMode == MediaSecurityMode.AuthenticatedSFrame && _sframe?.IsReady != true) return;
        var stream = _mediaClient?.GetMediaStream(_activeAudioStreamId);
        if (stream is not null)
        {
            try { await stream.SendFrameAsync(data, false); }
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
                        await PlayVideoFragmentAsync(stream.StreamId, frame.Data);
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
        _activeAudioStreamId = Guid.Empty;
        _activeVideoStreamId = Guid.Empty;
        _hasVideo = false;
        lock (_configuredCalls) _configuredCalls.Clear();

        if (_audio.IsCapturing) await _audio.StopCaptureAsync();
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
        if (_mediaClient is not null) await _mediaClient.DisposeAsync();
        // These dependencies belong to the DI scope; it disposes each once after this service.
        _initialized = false;
    }
}
