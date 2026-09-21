using Microsoft.AspNetCore.Components;

namespace Bolt.Media.Browser;

/// <summary>What the browser reported about this device's video encoders, before any camera is opened.</summary>
public sealed record VideoCapabilities(bool Supported, string? Reason, int Ceiling, VideoCodecProbe[] Codecs);
public sealed record VideoCodecProbe(string Codec, bool Encode, bool Decode, bool Hardware, int MaxHeight, int DecodeMaxHeight = 1080);
/// <summary>The camera actually acquired, which may differ from what was asked for.
/// <paramref name="Strategy"/> is how frames are read: "processor" or the "rvfc" fallback.</summary>
public sealed record VideoCaptureState(bool Capturing, string DeviceId, string FacingMode, int Width, int Height,
    string Codec, string Strategy = "");
public sealed record VideoSendStats(double Fps, double Kbps, int Dropped, int Backlog);

/// <summary>
/// Camera capture → WebCodecs encode → C# callback, and C# → per-sender WebCodecs decode → canvas.
///
/// The pipeline never opens a camera on its own: <see cref="StartCaptureAsync"/> is the only path
/// to getUserMedia, and the browser side releases the track on page hide, track end and dispose.
/// </summary>
public sealed class BoltVideoPipeline(IJSRuntime js, ILogger<BoltVideoPipeline> logger) : IAsyncDisposable
{
    private IJSObjectReference? module;
    private IJSObjectReference? pipeline;
    private DotNetObjectReference<BoltVideoPipeline>? self;
    private bool capturing;

    /// <summary>An encoded picture: payload, keyframe flag, sender-assigned frame ID, capture time in µs.</summary>
    public event Action<byte[], bool, uint, uint>? OnEncoded;
    /// <summary>The browser released the camera: "hidden", "ended", "denied" or "encoder".</summary>
    public event Action<string>? OnCaptureStopped;
    /// <summary>A remote decoder failed; the caller should ask that sender for a keyframe.</summary>
    public event Action<string>? OnDecodeFailed;

    public bool IsCapturing => capturing;

    private async Task<IJSObjectReference> ModuleAsync() => module ??=
        await js.InvokeAsync<IJSObjectReference>("import", "./_content/Bolt.Media.Browser/bolt-media.js");

    /// <summary>A participant who never turns a camera on still needs the pipeline to render others.</summary>
    private async Task<IJSObjectReference> PipelineAsync()
    {
        pipeline ??= await (await ModuleAsync()).InvokeAsync<IJSObjectReference>("createVideoPipeline");
        self ??= DotNetObjectReference.Create(this);
        return pipeline;
    }

    /// <summary>Probe encoders and decoders. Safe to call before a call: it opens no device.</summary>
    public async Task<VideoCapabilities> CheckCapabilitiesAsync()
        => await (await ModuleAsync()).InvokeAsync<VideoCapabilities>("checkVideoCapabilities");

    public async Task<MediaDeviceInfo[]> CamerasAsync()
        => await (await ModuleAsync()).InvokeAsync<MediaDeviceInfo[]>("enumerateVideoInputs");

    public async Task InitializeEncoderAsync(string codec, VideoTier tier, int keyframeSeconds = 2)
    {
        var active = await PipelineAsync();
        await active.InvokeAsync<object>("initEncoder", codec, tier.Width, tier.Height, tier.BitrateKbps, tier.Framerate, keyframeSeconds);
    }

    /// <summary>Opens the camera. The only call in this library that asks for video input.</summary>
    public async Task<VideoCaptureState> StartCaptureAsync(string? deviceId = null, string? facingMode = null)
    {
        if (pipeline is null) throw new InvalidOperationException("Initialize the video encoder first.");
        var state = await pipeline.InvokeAsync<VideoCaptureState>("startCapture", self, new { deviceId, facingMode });
        capturing = state.Capturing;
        logger.LogDebug("Camera started {Width}x{Height} {Codec} via {Strategy}", state.Width, state.Height, state.Codec, state.Strategy);
        return state;
    }

    public async Task StopCaptureAsync()
    {
        capturing = false;
        if (pipeline is not null) await pipeline.InvokeVoidAsync("stopCapture");
    }

    public async Task AttachPreviewAsync(ElementReference element)
        => await (await PipelineAsync()).InvokeVoidAsync("attachPreview", element);

    public async Task DetachPreviewAsync()
    {
        if (pipeline is not null) await pipeline.InvokeVoidAsync("attachPreview", null);
    }

    public async Task<bool> AddRemoteAsync(Guid streamId, ElementReference canvas, string codec)
        => await (await PipelineAsync()).InvokeAsync<bool>("addRemote", streamId.ToString("D"), canvas, codec, self);

    public async Task RemoveRemoteAsync(Guid streamId)
    {
        if (pipeline is not null) await pipeline.InvokeVoidAsync("removeRemote", streamId.ToString("D"));
    }

    public async ValueTask DecodeFrameAsync(Guid streamId, byte[] data, uint timestampMicroseconds, bool isKeyframe, bool discontinuity = false)
    {
        if (pipeline is null) return;
        await pipeline.InvokeAsync<bool>("decodeFrame", streamId.ToString("D"), data, timestampMicroseconds, isKeyframe, discontinuity);
    }

    public async ValueTask<bool> ApplyTierAsync(VideoTier tier)
        => pipeline is not null && await pipeline.InvokeAsync<bool>("applyTier", tier.Width, tier.Height, tier.BitrateKbps, tier.Framerate);

    public async ValueTask RequestKeyframeAsync()
    {
        if (pipeline is not null) await pipeline.InvokeVoidAsync("requestKeyframe");
    }

    public async ValueTask<VideoSendStats> StatsAsync()
        => pipeline is null ? new(0, 0, 0, 0) : await pipeline.InvokeAsync<VideoSendStats>("getStats");

    public async ValueTask<VideoDiagnostics?> DiagnosticsAsync(bool enabled)
        => pipeline is null ? null : await pipeline.InvokeAsync<VideoDiagnostics?>("getDiagnostics", enabled);

    [JSInvokable]
    public void OnVideoEncoded(byte[] data, bool isKeyframe, uint frameId, uint timestamp)
        => OnEncoded?.Invoke(data, isKeyframe, frameId, timestamp);

    [JSInvokable]
    public void OnVideoCaptureStopped(string reason)
    {
        capturing = false;
        OnCaptureStopped?.Invoke(reason);
    }

    [JSInvokable]
    public void OnVideoDecodeFailed(string streamId) => OnDecodeFailed?.Invoke(streamId);

    public async ValueTask DisposeAsync()
    {
        capturing = false;
        if (pipeline is not null)
        {
            try { await pipeline.InvokeVoidAsync("dispose"); } catch (JSException) { /* The page may already be gone. */ }
            await pipeline.DisposeAsync();
            pipeline = null;
        }
        self?.Dispose(); self = null;
        if (module is not null) { await module.DisposeAsync(); module = null; }
    }
}
