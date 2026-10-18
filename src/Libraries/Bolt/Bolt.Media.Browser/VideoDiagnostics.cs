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
    /// <summary>
    /// How the camera's orientation reaches the far side: "sent as metadata (90°)", "redrawn upright (2d canvas; 90°)",
    /// "painted upright by the element (webgl)" on the frame-callback path, or "none" for frames that arrive upright.
    /// </summary>
    public string Orientation { get; init; } = "";
    /// <summary>The media path: "UDP/relay", "TLS/relay", "WebSocket" and so on.</summary>
    public string Transport { get; init; } = "";
    /// <summary>Why media is on the WebSocket, when it is (no TURN offered, ICE failed, negotiating).</summary>
    public string? TransportReason { get; init; }
    public double? TransportRttMs { get; init; }
    /// <summary>Audio leaves with its previous frame alongside (the datagram path reports loss).</summary>
    public bool AudioRedundancy { get; init; }
    /// <summary>Video fragments retransmitted on this uplink since the current transport was created.</summary>
    public long UplinkResent { get; init; }
    /// <summary>Latest congestion decision, absent until the first rate-control window.</summary>
    public int? TotalBudgetKbps { get; init; }
    public int? AudioBitrateKbps { get; init; }
    public int? VideoBudgetKbps { get; init; }
    public int? QueueDelayMs { get; init; }
    public string? Congestion { get; init; }
    public bool VideoSuspended { get; init; }
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
    /// <summary>The sender's orientation applied when painting: "90°", "180°, mirrored" or "none".</summary>
    public string Rotation { get; init; } = "";
    /// <summary>Recovery counters since this stream appeared, not rates.</summary>
    public VideoReceiveStats? Recovery { get; init; }
}
