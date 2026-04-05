namespace Bolt.Media.Browser;

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

    /// <summary>Fires when the audio encoder produces an encoded Opus frame.</summary>
    public event Action<byte[]>? OnEncoded;

    public bool IsCapturing => _capturing;

    public BoltAudioPipeline(IJSRuntime js, ILogger<BoltAudioPipeline> logger)
    {
        _js = js;
        _logger = logger;
    }

    /// <summary>Load JS module, initialize Opus encoder and decoder.</summary>
    public async Task InitializeAsync(int sampleRate = 48_000, int channels = 1, int bitrateKbps = 64)
    {
        _module = await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Bolt.Media.Browser/bolt-media.js");
        _pipeline = await _module.InvokeAsync<IJSObjectReference>("createAudioPipeline");
        _dotNetRef = DotNetObjectReference.Create(this);

        await _pipeline.InvokeVoidAsync("initEncoder", sampleRate, channels, bitrateKbps);
        await _pipeline.InvokeVoidAsync("initDecoder", sampleRate, channels);
    }

    /// <summary>Start capturing audio from the microphone.</summary>
    public async Task StartCaptureAsync(int? sampleRate = null, int? channels = null)
    {
        if (_pipeline is null) throw new InvalidOperationException("Call InitializeAsync first");

        object? constraints = (sampleRate.HasValue || channels.HasValue)
            ? new { sampleRate = sampleRate ?? 48_000, channels = channels ?? 1 }
            : null;

        await _pipeline.InvokeVoidAsync("startCapture", _dotNetRef, constraints);
        _capturing = true;
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
    {
        if (_pipeline is null) return;
        await _pipeline.InvokeVoidAsync("decodeFrame", data.ToArray(), timestamp);
    }

    /// <summary>Change the encoder bitrate in response to ABR feedback.</summary>
    public async ValueTask ReconfigureBitrateAsync(int sampleRate, int channels, int newBitrateKbps)
    {
        if (_pipeline is null) return;
        await _pipeline.InvokeVoidAsync("reconfigureBitrate", sampleRate, channels, newBitrateKbps);
    }

    /// <summary>Called from JS when an encoded audio chunk is ready.</summary>
    [JSInvokable]
    public void OnAudioEncoded(byte[] data)
    {
        OnEncoded?.Invoke(data);
    }

    public async ValueTask DisposeAsync()
    {
        _capturing = false;
        if (_pipeline is not null)
        {
            await _pipeline.InvokeVoidAsync("dispose");
            await _pipeline.DisposeAsync();
        }
        _dotNetRef?.Dispose();
        if (_module is not null) await _module.DisposeAsync();
    }
}
