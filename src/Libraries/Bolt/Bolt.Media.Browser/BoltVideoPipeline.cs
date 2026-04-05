using Microsoft.AspNetCore.Components;

namespace Bolt.Media.Browser;

/// <summary>
/// Video capture → WebCodecs H.264 encode → C# callback,
/// and C# → WebCodecs decode → canvas rendering.
/// </summary>
public sealed class BoltVideoPipeline : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly ILogger<BoltVideoPipeline> _logger;
    private IJSObjectReference? _module;
    private IJSObjectReference? _pipeline;
    private DotNetObjectReference<BoltVideoPipeline>? _dotNetRef;
    private bool _capturing;

    /// <summary>Fires when the video encoder produces an encoded H.264 frame.</summary>
    public event Action<byte[], bool>? OnEncoded;

    public bool IsCapturing => _capturing;

    public BoltVideoPipeline(IJSRuntime js, ILogger<BoltVideoPipeline> logger)
    {
        _js = js;
        _logger = logger;
    }

    /// <summary>Load JS module, initialize H.264 encoder.</summary>
    public async Task InitializeEncoderAsync(
        int width = 1280, int height = 720, int bitrateKbps = 2_000,
        int framerate = 30, string codec = "h264", int keyframeInterval = 60)
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Bolt.Media.Browser/bolt-media.js");
        _pipeline ??= await _module.InvokeAsync<IJSObjectReference>("createVideoPipeline");
        _dotNetRef ??= DotNetObjectReference.Create(this);

        await _pipeline.InvokeVoidAsync("initEncoder", width, height, bitrateKbps, framerate, codec, keyframeInterval);
    }

    /// <summary>Initialize decoder and attach to a canvas element for rendering.</summary>
    public async Task InitializeDecoderAsync(ElementReference canvasElement, string codec = "h264")
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Bolt.Media.Browser/bolt-media.js");
        _pipeline ??= await _module.InvokeAsync<IJSObjectReference>("createVideoPipeline");

        await _pipeline.InvokeVoidAsync("initDecoder", canvasElement, codec);
    }

    /// <summary>Start capturing video from the camera.</summary>
    public async Task StartCaptureAsync(int? width = null, int? height = null, int? framerate = null)
    {
        if (_pipeline is null) throw new InvalidOperationException("Call InitializeEncoderAsync first");

        object? constraints = (width.HasValue || height.HasValue || framerate.HasValue)
            ? new { width = width ?? 1280, height = height ?? 720, framerate = framerate ?? 30 }
            : null;

        await _pipeline.InvokeVoidAsync("startCapture", _dotNetRef, constraints);
        _capturing = true;
        _logger.LogDebug("Video capture started");
    }

    /// <summary>Stop capturing video.</summary>
    public async Task StopCaptureAsync()
    {
        if (_pipeline is null) return;
        await _pipeline.InvokeVoidAsync("stopCapture");
        _capturing = false;
    }

    /// <summary>Decode and render an incoming video frame to the canvas.</summary>
    public async ValueTask DecodeFrameAsync(ReadOnlyMemory<byte> data, uint timestamp, bool isKeyframe)
    {
        if (_pipeline is null) return;
        await _pipeline.InvokeVoidAsync("decodeFrame", data.ToArray(), timestamp, isKeyframe);
    }

    /// <summary>Request the encoder to produce a keyframe on the next encode cycle.</summary>
    public async ValueTask RequestKeyframeAsync()
    {
        if (_pipeline is null) return;
        await _pipeline.InvokeVoidAsync("requestKeyframe");
    }

    /// <summary>Change encoder bitrate for ABR.</summary>
    public async ValueTask ReconfigureBitrateAsync(int newBitrateKbps)
    {
        if (_pipeline is null) return;
        await _pipeline.InvokeVoidAsync("reconfigureBitrate", newBitrateKbps);
    }

    /// <summary>Change encoder resolution/framerate for ABR.</summary>
    public async ValueTask ReconfigureResolutionAsync(int width, int height, int? framerate = null)
    {
        if (_pipeline is null) return;
        await _pipeline.InvokeVoidAsync("reconfigureResolution", width, height, framerate);
    }

    [JSInvokable]
    public void OnVideoEncoded(byte[] data, bool isKeyframe)
    {
        OnEncoded?.Invoke(data, isKeyframe);
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
