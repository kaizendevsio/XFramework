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
public sealed class BoltMediaService : IAsyncDisposable
{
    private readonly BoltCryptoInterop _crypto;
    private readonly BoltAudioPipeline _audio;
    private readonly BoltVideoPipeline _video;
    private readonly BoltDeviceManager _devices;
    private readonly MediaServiceOptions _options;
    private readonly ILogger<BoltMediaService> _logger;

    private BoltMediaClient? _mediaClient;
    private readonly Dictionary<Guid, CancellationTokenSource> _streamPlaybackTasks = new();

    private Guid _activeAudioStreamId;
    private Guid _activeVideoStreamId;
    private bool _hasVideo;
    private bool _initialized;

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

    public BoltMediaService(
        BoltCryptoInterop crypto,
        BoltAudioPipeline audio,
        BoltVideoPipeline video,
        BoltDeviceManager devices,
        MediaServiceOptions options,
        ILogger<BoltMediaService> logger)
    {
        _crypto = crypto;
        _audio = audio;
        _video = video;
        _devices = devices;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Initialize the media service. Call once after BoltClient is connected.
    /// Loads JS modules, generates encryption keys, wires call events.
    /// </summary>
    public async Task InitializeAsync(BoltClient client)
    {
        if (_initialized) return;

        // Initialize crypto if encryption enabled
        if (_options.EnableEncryption)
            await _crypto.InitializeAsync();

        // Initialize audio pipeline
        await _audio.InitializeAsync(_options.AudioSampleRate, _options.AudioChannels, _options.AudioBitrateKbps);

        // Create media client and wire events
        _mediaClient = new BoltMediaClient(client, _logger);

        if (_options.EnableEncryption)
            _mediaClient.SetEncryptionFactory(() => _crypto.CreateEncryption());

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
        _mediaClient.OnKeyframeRequested += streamId =>
        {
            _ = _video.RequestKeyframeAsync();
        };

        _initialized = true;
        _logger.LogInformation("BoltMediaService initialized");
    }

    // ── Call API ──

    /// <summary>Start a voice or voice+video call to a recipient.</summary>
    public async Task<Guid> StartCallAsync(string recipientId, bool video = false)
    {
        EnsureInitialized();

        _hasVideo = video;

        _audio.OnEncoded -= OnAudioEncodedForStream;
        _audio.OnEncoded += OnAudioEncodedForStream;

        if (video)
        {
            _video.OnEncoded -= OnVideoEncodedForStream;
            _video.OnEncoded += OnVideoEncodedForStream;
        }

        var callId = await _mediaClient!.StartCallAsync(recipientId, video, _options.EnableEncryption);
        _logger.LogInformation("Call started: {CallId} to {Recipient}, video={Video}", callId, recipientId, video);
        return callId;
    }

    /// <summary>Answer an incoming call.</summary>
    public async Task AnswerCallAsync(Guid callId, bool video = false)
    {
        EnsureInitialized();

        _hasVideo = video;

        _audio.OnEncoded -= OnAudioEncodedForStream;
        _audio.OnEncoded += OnAudioEncodedForStream;

        if (video)
        {
            _video.OnEncoded -= OnVideoEncodedForStream;
            _video.OnEncoded += OnVideoEncodedForStream;
        }

        await _mediaClient!.AnswerCallAsync(callId);
    }

    /// <summary>Reject an incoming call.</summary>
    public async Task RejectCallAsync(Guid callId)
    {
        EnsureInitialized();
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

    /// <summary>
    /// Start capturing and sending video. Call after the call is answered.
    /// Pass a canvas ElementReference for remote video rendering.
    /// </summary>
    public async Task StartVideoAsync(ElementReference remoteVideoCanvas)
    {
        await _video.InitializeEncoderAsync(
            _options.VideoWidth, _options.VideoHeight, _options.VideoBitrateKbps,
            _options.VideoFramerate, _options.VideoCodec, _options.KeyframeIntervalFrames);

        await _video.InitializeDecoderAsync(remoteVideoCanvas, _options.VideoCodec);

        await _video.StartCaptureAsync(_options.VideoWidth, _options.VideoHeight, _options.VideoFramerate);
    }

    /// <summary>Stop audio capture (mute).</summary>
    public async Task StopAudioAsync() => await _audio.StopCaptureAsync();

    /// <summary>Stop video capture (camera off).</summary>
    public async Task StopVideoAsync() => await _video.StopCaptureAsync();

    // ── Internal Wiring ──

    private async Task HandleCallAnsweredAsync(Guid callId)
    {
        var client = _mediaClient!.Client;
        var conn = client.GetPrimaryConnection();
        var writer = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();

        // Send audio MediaConfig to peer
        _activeAudioStreamId = Guid.NewGuid();
        BoltCodec.WriteMediaConfig(writer, _activeAudioStreamId, callId, MediaType.Audio, CodecId.Opus,
            _options.AudioSampleRate, _options.AudioChannels, _options.AudioBitrateKbps, 0, ReadOnlySpan<byte>.Empty);
        await conn.SendAsync(writer.WrittenMemory, CancellationToken.None);
        writer.Reset();

        // Create local audio stream with features enabled
        var audioStream = new BoltMediaStream(conn, _activeAudioStreamId, callId, true);
        if (_options.EnableFec) audioStream.EnableFec(_options.FecAudioGroupSize);
        audioStream.EnableNack(128);
        audioStream.EnableVad();
        audioStream.EnablePlc();
        audioStream.OnBitrateChanged += kbps =>
            _ = _audio.ReconfigureBitrateAsync(_options.AudioSampleRate, _options.AudioChannels, kbps);

        // Start playback loop for received audio frames
        StartPlaybackLoop(audioStream);

        // Send video MediaConfig if video is active for this call
        if (_hasVideo || _video.IsCapturing)
        {
            _activeVideoStreamId = Guid.NewGuid();
            BoltCodec.WriteMediaConfig(writer, _activeVideoStreamId, callId, MediaType.Video, CodecId.H264,
                _options.VideoWidth, _options.VideoHeight, _options.VideoBitrateKbps, 0, ReadOnlySpan<byte>.Empty);
            await conn.SendAsync(writer.WrittenMemory, CancellationToken.None);
            writer.Reset();

            var videoStream = new BoltMediaStream(conn, _activeVideoStreamId, callId, false);
            if (_options.EnableFec) videoStream.EnableFec(_options.FecVideoGroupSize);
            videoStream.EnableNack(256);
            videoStream.EnableBandwidthProbing(_options.VideoBitrateKbps);
            videoStream.OnBitrateChanged += kbps => _ = _video.ReconfigureBitrateAsync(kbps);
            videoStream.OnKeyframeNeeded += () => _ = _video.RequestKeyframeAsync();

            StartPlaybackLoop(videoStream);
        }

        if (OnCallAnswered is not null) await OnCallAnswered(callId);
    }

    private void OnAudioEncodedForStream(byte[] data)
    {
        var stream = _mediaClient?.GetMediaStream(_activeAudioStreamId);
        if (stream is not null)
            _ = stream.SendFrameAsync(data, false);
    }

    private void OnVideoEncodedForStream(byte[] data, bool isKeyframe)
    {
        var stream = _mediaClient?.GetMediaStream(_activeVideoStreamId);
        if (stream is not null)
            _ = stream.SendFrameAsync(data, isKeyframe);
    }

    private void StartPlaybackLoop(BoltMediaStream stream)
    {
        var cts = new CancellationTokenSource();
        _streamPlaybackTasks[stream.StreamId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var frame in stream.ReadFramesAsync(cts.Token))
                {
                    if (stream.IsAudio)
                        await _audio.DecodeFrameAsync(frame.Data, frame.Timestamp);
                    else
                        await _video.DecodeFrameAsync(frame.Data, frame.Timestamp, frame.IsKeyframe);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Playback loop error for stream {StreamId}", stream.StreamId);
            }
        });
    }

    private async Task StopPipelinesAsync()
    {
        foreach (var (_, cts) in _streamPlaybackTasks)
        {
            await cts.CancelAsync();
            cts.Dispose();
        }
        _streamPlaybackTasks.Clear();

        if (_audio.IsCapturing) await _audio.StopCaptureAsync();
        if (_video.IsCapturing) await _video.StopCaptureAsync();
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("Call InitializeAsync(boltClient) first");
    }

    public async ValueTask DisposeAsync()
    {
        await StopPipelinesAsync();
        if (_mediaClient is not null) await _mediaClient.DisposeAsync();
        await _audio.DisposeAsync();
        await _video.DisposeAsync();
        await _crypto.DisposeAsync();
        await _devices.DisposeAsync();
    }
}
