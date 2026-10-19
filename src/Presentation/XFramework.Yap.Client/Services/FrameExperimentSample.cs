using Yap.Contracts;
using M = Yap.Contracts.CallMetric;

namespace Yap.Client.Services;

/// <summary>Numeric-only local camera experiment. No pixels, device labels or media are uploaded.</summary>
public sealed record FrameExperimentSample
{
    public int Mode { get; init; }
    public int Rear { get; init; }
    public string Codec { get; init; } = "";
    public double? ElapsedMs { get; init; }
    public double? RequestedHeight { get; init; }
    public double? RequestedFps { get; init; }
    public double? CameraWidth { get; init; }
    public double? CameraHeight { get; init; }
    public double? CameraFps { get; init; }
    public double? Width { get; init; }
    public double? Height { get; init; }
    public double? DisplayWidth { get; init; }
    public double? DisplayHeight { get; init; }
    public double? Rotation { get; init; }
    public double? Flip { get; init; }
    public double? EncoderWidth { get; init; }
    public double? EncoderHeight { get; init; }
    public double? DecodedWidth { get; init; }
    public double? DecodedHeight { get; init; }
    public double? CaptureFps { get; init; }
    public double? EncodedFps { get; init; }
    public double? RenderedFps { get; init; }
    public double? EncodedKbps { get; init; }
    public double? Drops { get; init; }
    public double? PrepareMs { get; init; }
    public double? PrepareP95Ms { get; init; }
    public double? PaintMs { get; init; }
    public double? SnapshotMs { get; init; }
    public double? EncodeMs { get; init; }
    public double? DecodeMs { get; init; }
    public double? RenderMs { get; init; }
    public double? EncoderQueue { get; init; }
    public double? DecoderQueue { get; init; }
    public double? ScreenAngle { get; init; }

    public Dictionary<CallMetric, double?> Metrics() => new()
    {
        [M.FrameExperiment] = Mode, [M.RearCamera] = Rear, [M.ElapsedMs] = ElapsedMs, [M.RequestedHeight] = RequestedHeight, [M.RequestedFps] = RequestedFps,
        [M.CameraWidth] = CameraWidth, [M.CameraHeight] = CameraHeight, [M.CameraFps] = CameraFps,
        [M.RawWidth] = Width, [M.RawHeight] = Height, [M.DisplayWidth] = DisplayWidth, [M.DisplayHeight] = DisplayHeight,
        [M.FrameRotation] = Rotation, [M.FrameFlip] = Flip, [M.Width] = EncoderWidth, [M.Height] = EncoderHeight,
        [M.DecodedWidth] = DecodedWidth, [M.DecodedHeight] = DecodedHeight, [M.CaptureFps] = CaptureFps,
        [M.EncodedFps] = EncodedFps, [M.RenderedFps] = RenderedFps, [M.EncodedKbps] = EncodedKbps, [M.CaptureDrops] = Drops,
        [M.CapturePrepareMs] = PrepareMs, [M.PrepareP95Ms] = PrepareP95Ms, [M.CapturePaintMs] = PaintMs,
        [M.CaptureFrameMs] = SnapshotMs, [M.EncoderDelayMs] = EncodeMs, [M.DecoderDelayMs] = DecodeMs,
        [M.RenderMs] = RenderMs, [M.EncoderQueue] = EncoderQueue, [M.DecoderQueue] = DecoderQueue, [M.ScreenAngle] = ScreenAngle
    };
}
