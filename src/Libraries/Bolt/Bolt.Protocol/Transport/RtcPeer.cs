namespace Bolt.Protocol.Transport;

/// <summary>One STUN/TURN server, in the shape WebRTC configurations take.</summary>
public sealed record RtcIceServer(string[] Urls, string? Username = null, string? Credential = null)
{
    /// <summary>Never print credentials: logs and exceptions see the URLs only.</summary>
    public override string ToString() => string.Join(",", Urls);
}

/// <summary>
/// The path a data channel settled on: candidate types on each side (host, srflx, prflx, relay), the local
/// transport protocol, and for a local relay candidate the protocol to the TURN server (udp, tcp, tls).
/// </summary>
public sealed record RtcPath(string Local, string LocalProtocol, string? RelayProtocol, string Remote, double RttMs)
{
    /// <summary>"UDP/relay", "TLS/relay" and so on: the leg this side controls, as people read it.</summary>
    public string Describe()
    {
        var protocol = (RelayProtocol is { Length: > 0 } relay && Local == "relay" ? relay : LocalProtocol).ToUpperInvariant();
        return $"{protocol}/{Local}";
    }

    /// <summary>True when this side's leg rides TCP or TLS (to the TURN server or directly).</summary>
    public bool IsStreamBased =>
        LocalProtocol.Equals("tcp", StringComparison.OrdinalIgnoreCase) ||
        RelayProtocol is { } relay && (relay.Equals("tcp", StringComparison.OrdinalIgnoreCase) || relay.Equals("tls", StringComparison.OrdinalIgnoreCase));
}

/// <summary>One trickled ICE candidate. An empty <see cref="Candidate"/> means gathering finished.</summary>
public sealed record RtcCandidate(string Candidate, string? SdpMid, int? SdpMLineIndex);

public enum RtcPeerRole { Offer, Answer }

public enum RtcChannelState
{
    Connecting,
    Open,
    Closed,
    Failed,
    /// <summary>
    /// The channel is open but the path under it carries nothing right now: ICE has stopped hearing from the
    /// other side, or is checking again after a restart. Nothing is sent on it (media takes the WebSocket); it
    /// goes back to <see cref="Open"/> when ICE is connected again, or ends <see cref="Failed"/> or <see cref="Closed"/>.
    /// </summary>
    Stalled,
}

/// <param name="IceServers">STUN/TURN servers for this peer.</param>
/// <param name="RelayOnly">Gather and use relay candidates only (iceTransportPolicy "relay").</param>
/// <param name="MaxMessageBytes">Largest data-channel message either way; larger ones are not sent.</param>
/// <param name="MinCwndBytes">SCTP congestion-window floor (0 keeps SCTP's own), see the remarks on <see cref="IRtcPeer"/>.</param>
/// <param name="AllowLoopback">Tests only: offer loopback candidates.</param>
public sealed record RtcPeerOptions(
    IReadOnlyList<RtcIceServer> IceServers,
    bool RelayOnly,
    int MaxMessageBytes = RtcDefaults.MaxMessageBytes,
    int MinCwndBytes = 0,
    bool AllowLoopback = false);

public static class RtcDefaults
{
    /// <summary>
    /// The largest media message on the datagram path: one SCTP DATA chunk inside one DTLS record inside one
    /// UDP datagram (TURN-wrapped on both legs) stays under a 1280-byte IPv6 minimum MTU. Bigger messages
    /// would be split by SCTP, and losing any piece of an unretransmitted message loses all of it.
    /// </summary>
    public const int MaxMessageBytes = 1150;
}

/// <summary>
/// A WebRTC peer connection with one data channel ("bolt-media", unordered, never retransmitted, binary),
/// opened in band by the offering side and accepted, with exactly those settings, by the answering side.
/// Media rides it as whole Bolt frames, one per message, already SFrame-encrypted end to end; DTLS adds hop
/// encryption only.
///
/// Congestion: SCTP's own loss-based window still runs underneath. A sender reads it through
/// <see cref="BufferedAmount"/> (what the channel has not yet sent or had acknowledged) and keeps that
/// short, so priority and dropping stay above, in the media queues; it never reacts to loss twice.
/// </summary>
public interface IRtcPeer : IAsyncDisposable
{
    RtcPeerRole Role { get; }
    RtcChannelState State { get; }
    /// <summary>The selected candidate pair, once known.</summary>
    RtcPath? Path { get; }
    int MaxMessageBytes { get; }
    /// <summary>Bytes handed to the channel and not yet sent (or, on some implementations, not yet acknowledged).</summary>
    long BufferedAmount { get; }
    /// <summary>The SCTP congestion window when the implementation knows it, else 0.</summary>
    long CongestionWindow { get; }
    /// <summary>Messages the channel or the layer below refused or dropped.</summary>
    long Dropped { get; }

    /// <summary>Offerer: create (or, for an ICE restart, recreate) the local offer.</summary>
    Task<string> CreateOfferAsync(bool iceRestart, CancellationToken ct);
    /// <summary>Answerer: apply a remote offer and return the answer.</summary>
    Task<string> AnswerAsync(string offerSdp, CancellationToken ct);
    /// <summary>Offerer: apply the remote answer.</summary>
    Task SetAnswerAsync(string answerSdp, CancellationToken ct);
    Task AddCandidateAsync(RtcCandidate candidate, CancellationToken ct);

    /// <summary>
    /// Queue one message without waiting. False when the channel is not open or would not take it; the caller
    /// sends that frame another way or drops it.
    /// </summary>
    bool TrySend(ReadOnlySpan<byte> message);

    event Action<RtcCandidate>? LocalCandidate;
    event Action<RtcChannelState>? StateChanged;
    /// <summary>One inbound message. The memory is only valid for the duration of the call.</summary>
    event Action<ReadOnlyMemory<byte>>? Message;
    /// <summary>The buffered amount went down: a sender holding media back may continue.</summary>
    event Action? BufferedAmountLow;
    /// <summary>The path or its RTT changed.</summary>
    event Action<RtcPath>? PathChanged;
}

public interface IRtcPeerFactory
{
    ValueTask<IRtcPeer> CreateAsync(RtcPeerRole role, RtcPeerOptions options, CancellationToken ct);
}

/// <summary>Which Bolt frames may travel on the datagram path, in each direction.</summary>
public static class DatagramFramePolicy
{
    /// <summary>
    /// Participant to relay: media and the receiver's own feedback. Everything that changes state (stream
    /// configuration, signals, registration, heartbeats) stays on the authenticated stream connection.
    /// </summary>
    public static bool AcceptFromParticipant(FrameType type) =>
        type is FrameType.MediaFrame or FrameType.FecFrame or FrameType.MediaFeedback or
            FrameType.MediaKeyRequest or FrameType.MediaBundle or FrameType.NackRequest or FrameType.TransportSequenced;

    /// <summary>Relay to participant: what the relay's media lanes carry (media, feedback, congestion reports, keyframe requests).</summary>
    public static bool AcceptFromRelay(FrameType type) =>
        type is FrameType.MediaFrame or FrameType.FecFrame or FrameType.MediaFeedback or
            FrameType.MediaKeyRequest or FrameType.MediaCongestion or FrameType.MediaBundle or FrameType.NackRequest or
            FrameType.NackDeclined or FrameType.TransportFeedback;
}
