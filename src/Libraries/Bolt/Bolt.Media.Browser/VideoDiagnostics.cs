namespace Bolt.Media.Browser;

/// <summary>Local measurements only. Acceleration is a browser preference, not proof of hardware use.</summary>
public sealed record VideoDiagnostics
{
    public bool Capturing { get; init; }
    public string Strategy { get; init; } = "";
    public string Codec { get; init; } = "";
    public string Acceleration { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    public double TargetFps { get; init; }
    public int CameraWidth { get; init; }
    public int CameraHeight { get; init; }
    public double? CameraFps { get; init; }
    public double? CaptureFps { get; init; }
    public double? EncodedFps { get; init; }
    public double? EncodedKbps { get; init; }
    public int EncoderQueue { get; init; }
    public int SendQueue { get; init; }
    public double? EncoderDelayMs { get; init; }
    public int Dropped { get; init; }
    public RemoteVideoDiagnostics[] Remotes { get; init; } = [];
}

public sealed record RemoteVideoDiagnostics
{
    public Guid StreamId { get; init; }
    public string Codec { get; init; } = "";
    public string Acceleration { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    public double? ReceivedFps { get; init; }
    public double? RenderedFps { get; init; }
    public double? ReceivedKbps { get; init; }
    public int DecoderQueue { get; init; }
    public int PendingFrames { get; init; }
    public double? DecoderDelayMs { get; init; }
    public int Resets { get; init; }
}
