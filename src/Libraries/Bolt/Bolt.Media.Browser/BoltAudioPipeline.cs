namespace Bolt.Media.Browser;

public sealed record VoiceCapabilities(bool Supported, string? Reason, bool NativeCodecs = false);

/// <summary>
/// Audio capture → WebCodecs encode → C# callback, and C# → WebCodecs decode → AudioContext playback.
/// Bridges browser audio APIs to <see cref="BoltMediaStream"/>.
/// </summary>
public sealed class BoltAudioPipeline : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly ILogger<BoltAudioPipeline> _logger;
    private IJSObjectReference? _module;
    private IJSObjectReference? _pipeline;
    private DotNetObjectReference<BoltAudioPipeline>? _dotNetRef;
    private bool _capturing;
    private ManagedOpusCodec? _managedCodec;
    private readonly Dictionary<Guid, ManagedOpusDecoder> _remoteCodecs = [];
    private const int MaxRemoteStreams = 8;

    /// <summary>Fires when the audio encoder produces an encoded Opus frame.</summary>
    public event Func<byte[], Task>? OnEncoded;

    public bool IsCapturing => _capturing;

    /// <summary>Checks codec support without opening the microphone or starting a call.</summary>
    public async Task<VoiceCapabilities> CheckCapabilitiesAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Bolt.Media.Browser/bolt-media.js");
        return await _module.InvokeAsync<VoiceCapabilities>("checkVoiceCapabilities");
    }

    public BoltAudioPipeline(IJSRuntime js, ILogger<BoltAudioPipeline> logger)
    {
        _js = js;
        _logger = logger;
    }

    /// <summary>Load JS module, initialize Opus encoder and decoder.</summary>
    public async Task InitializeAsync(int sampleRate = 48_000, int channels = 1, int bitrateKbps = 128)
    {
        if (_pipeline is not null) return;
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Bolt.Media.Browser/bolt-media.js");
        await _module.InvokeVoidAsync("requireSecureVoiceContext");
        _pipeline = await _module.InvokeAsync<IJSObjectReference>("createAudioPipeline");
        _dotNetRef = DotNetObjectReference.Create(this);

        try
        {
            var capabilities = await CheckCapabilitiesAsync();
            if (!capabilities.Supported) throw new NotSupportedException(capabilities.Reason);
            if (capabilities.NativeCodecs)
            {
                await _pipeline.InvokeVoidAsync("initEncoder", sampleRate, channels, bitrateKbps);
                await _pipeline.InvokeVoidAsync("initDecoder", sampleRate, channels);
            }
            else
            {
                if (sampleRate != 48_000 || channels != 1)
                    throw new NotSupportedException("Managed voice requires 48 kHz mono audio.");
                _managedCodec = new ManagedOpusCodec(bitrateKbps);
                await _pipeline.InvokeVoidAsync("initManaged");
            }
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    /// <summary>Start capturing audio from the microphone.</summary>
    public async Task StartCaptureAsync(int? sampleRate = null, int? channels = null, bool transmit = true)
    {
        if (_pipeline is null) throw new InvalidOperationException("Call InitializeAsync first");

        object? constraints = (sampleRate.HasValue || channels.HasValue)
            ? new { sampleRate = sampleRate ?? 48_000, channels = channels ?? 1 }
            : null;

        _capturing = await _pipeline.InvokeAsync<bool>("startCapture", _dotNetRef, constraints, transmit);
        _logger.LogDebug("Audio capture started");
    }

    /// <summary>Stop capturing audio.</summary>
    public async Task StopCaptureAsync()
    {
        if (_pipeline is null) return;
        await _pipeline.InvokeVoidAsync("stopCapture");
        _capturing = false;
        _logger.LogDebug("Audio capture stopped");
    }

    /// <summary>Decode and play an incoming audio frame from the remote peer.</summary>
    public async ValueTask DecodeFrameAsync(ReadOnlyMemory<byte> data, uint timestamp)
        => await DecodeFrameAsync(Guid.Empty, data, timestamp);

    /// <summary>Each sender has independent Opus history and playback scheduling.</summary>
    public async ValueTask DecodeFrameAsync(Guid streamId, ReadOnlyMemory<byte> data, uint timestamp)
    {
        if (_pipeline is null) return;
        if (_managedCodec is not null)
        {
            byte[] pcm;
            lock (_remoteCodecs)
            {
                if (!_remoteCodecs.TryGetValue(streamId, out var codec))
                {
                    if (_remoteCodecs.Count >= MaxRemoteStreams) return;
                    codec = new ManagedOpusDecoder();
                    _remoteCodecs.Add(streamId, codec);
                }
                pcm = codec.Decode(data.Span);
            }
            await _pipeline.InvokeVoidAsync("playPcm", pcm, streamId.ToString());
        }
        else
            await _pipeline.InvokeVoidAsync("decodeFrame", data.ToArray(), timestamp, streamId.ToString());
    }

    public async Task ReleaseRemoteStreamAsync(Guid streamId)
    {
        lock (_remoteCodecs)
            if (_remoteCodecs.Remove(streamId, out var codec)) codec.Dispose();
        if (_pipeline is not null) await _pipeline.InvokeVoidAsync("removeRemoteStream", streamId.ToString());
    }

    /// <summary>Change the encoder bitrate in response to ABR feedback.</summary>
    public async ValueTask ReconfigureBitrateAsync(int sampleRate, int channels, int newBitrateKbps)
    {
        if (_pipeline is null) return;
        if (_managedCodec is not null)
        {
            _managedCodec.SetBitrate(newBitrateKbps);
            return;
        }
        await _pipeline.InvokeVoidAsync("reconfigureBitrate", sampleRate, channels, newBitrateKbps);
    }

    /// <summary>Called from JS when an encoded audio chunk is ready.</summary>
    [JSInvokable]
    public async Task OnAudioEncoded(byte[] data)
    {
        if (OnEncoded is not { } handlers) return;
        foreach (Func<byte[], Task> handler in handlers.GetInvocationList())
            await handler(data);
    }

    [JSInvokable]
    public Task OnAudioPcm(byte[] pcm) => _managedCodec is null
        ? Task.CompletedTask
        : OnAudioEncoded(_managedCodec.Encode(pcm));

    public async Task StopPlaybackAsync()
    {
        ReleaseRemoteCodecs();
        if (_pipeline is not null) await _pipeline.InvokeVoidAsync("stopPlayback");
    }

    public async ValueTask DisposeAsync()
    {
        _capturing = false;
        _managedCodec?.Dispose();
        _managedCodec = null;
        ReleaseRemoteCodecs();
        if (_pipeline is not null)
        {
            await _pipeline.InvokeVoidAsync("dispose");
            await _pipeline.DisposeAsync();
        }
        _dotNetRef?.Dispose();
        if (_module is not null) await _module.DisposeAsync();
        _pipeline = null;
        _module = null;
        _dotNetRef = null;
    }

    private void ReleaseRemoteCodecs()
    {
        lock (_remoteCodecs)
        {
            foreach (var codec in _remoteCodecs.Values) codec.Dispose();
            _remoteCodecs.Clear();
        }
    }
}
