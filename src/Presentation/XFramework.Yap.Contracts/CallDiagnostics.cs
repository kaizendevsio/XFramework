namespace Yap.Contracts;

// Deliberately numeric, except bounded codec/version labels. No names, addresses, tokens or media.
public enum CallMetric
{
    Visible, ElapsedMs, RequestedHeight, RequestedFps, Capturing, CameraWidth, CameraHeight, CameraFps,
    Width, Height, TargetFps, CaptureFps, EncodedFps, EncodedKbps, EncoderQueue, SendQueue, EncoderDelayMs,
    CaptureDrops, CapturePrepareMs, EncodedCopyMs, EventLoopLagMs, RttMs, UplinkResent,
    TotalBudgetKbps, VideoBudgetKbps, AudioBitrateKbps, QueueDelayMs, VideoSuspended,
    LocalDelayMs, TransportDelayMs, RelayDelayMs, ReceiverDelayMs, UplinkLossPercent, CapacityKbps,
    RateCause, LastCutCause, LastCutFromKbps, LastCutToKbps, LastCutAgeMs,
    CryptoOperations, CryptoProcessingMs, CryptoWaitingMs, ReceivedFps, RenderedFps, ReceivedKbps,
    DecoderQueue, PendingFrames, RenderMs, DecoderDelayMs, Resets, Fragments, LocalDrops,
    Nacked, Recovered, Abandoned, Declined, Incomplete, Skipped, Pictures, RecoveryMs,
    CapturePaintMs, CaptureFrameMs,
    FrameExperiment, RawWidth, RawHeight, DisplayWidth, DisplayHeight, FrameRotation, FrameFlip,
    DecodedWidth, DecodedHeight, PrepareP95Ms, ScreenAngle, RearCamera,
    WaitingForKeyframe, RecoveryRequests, Topology, DelaySource, ReceiverAgeMs, TransportAgeMs, SentKbps, ApplicationLimited,
    CutLocalMs, CutTransportMs, CutReceiverMs, CutReceiverAgeMs, CutTransportAgeMs, CutSentKbps, CutCapacityKbps, CutLossPercent, CutGradient, CutAtMs,
    CameraMinFps, CameraMaxFps, CameraConstraintRejected, DecodedFps, DrawGapP95Ms, DrawGapMaxMs, PlayoutMs, PlayoutQueue, PlayoutDropped
}
public enum CallDevice { Other, Android, Ios, Desktop }
public enum CallCapturePath { Unknown, Processor, WorkerProcessor, WorkerWebgl, Rvfc, RvfcWebgl, WorkerWebgpu }
public enum CallTransportPath { Unknown, Udp, Tcp, Tls, WebSocket }
public sealed record CallDiagnosticRemote(Guid StreamId, string Codec, Dictionary<CallMetric, double?> Metrics);
public sealed record CallDiagnosticSample(Guid CallId, int Sequence, CallDevice Device, string Version,
    CallCapturePath CapturePath, CallTransportPath Transport, CallTransportPath RelayTransport,
    string Codec, Dictionary<CallMetric, double?> Metrics, CallDiagnosticRemote[] Remotes)
{
    public Dictionary<CallMetric, double?>[] Cuts { get; init; } = [];
}
public sealed record CallDiagnosticSession(Guid Id, DateTimeOffset ExpiresAt);
