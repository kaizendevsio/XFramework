using System.Security.Claims;
using Bolt.Protocol.Transport;

namespace Bolt.Server;

/// <summary>
/// Short-lived ICE servers for one participant's datagram session: <see cref="Client"/> goes to the
/// participant's device, <see cref="Server"/> stays with the relay's own peer and is never sent anywhere.
/// </summary>
public sealed record BoltIceGrant(IReadOnlyList<RtcIceServer> Client, IReadOnlyList<RtcIceServer> Server, DateTimeOffset ExpiresAt);

/// <summary>
/// Issues ICE servers (typically TURN credentials minted per participant). Return null when there are
/// none to give (not configured, or the provider is down): the participant then stays on its WebSocket,
/// exactly as before datagram paths existed.
/// </summary>
public interface IBoltIceServerSource
{
    ValueTask<BoltIceGrant?> GrantAsync(ClaimsPrincipal participant, CancellationToken ct);
}

/// <summary>
/// Lets participants of authenticated calls move their media onto a WebRTC data channel (UDP, through
/// TURN), with their WebSocket kept for everything else and as the fallback.
/// </summary>
public sealed class BoltMediaTransportOptions
{
    public required IRtcPeerFactory Peers { get; init; }
    public required IBoltIceServerSource IceServers { get; init; }

    /// <summary>
    /// The relay's own peer uses relay candidates only. A host behind carrier-grade NAT has no useful host
    /// or server-reflexive candidate (its public address is shared and changes), and TURN keeps the path
    /// outbound-only from the host. Participants may use any candidate type to reach that relay address.
    /// </summary>
    public bool RelayOnly { get; init; } = true;

    /// <summary>Largest data-channel message. See <see cref="RtcDefaults.MaxMessageBytes"/>.</summary>
    public int MaxMessageBytes { get; init; } = RtcDefaults.MaxMessageBytes;

    /// <summary>
    /// SCTP congestion-window floor on the relay's side. The relay's media lanes, its reports to senders and
    /// the senders' delay-based controllers decide the rate; SCTP's loss-based window would otherwise halve on
    /// every random loss and, at a second of RTT, cap a call below its audio. 128 KiB covers 1 Mbps at 1 s RTT.
    /// </summary>
    public int MinCwndBytes { get; init; } = 128 * 1024;

    /// <summary>Fewest seconds between two session requests from one connection.</summary>
    public int RequestSpacingSeconds { get; init; } = 2;

    /// <summary>Most session requests one connection may make (each may mint credentials).</summary>
    public int MaxRequestsPerConnection { get; init; } = 30;

    /// <summary>A session is renewed (new credentials, new peer, then the old one closes) this long before its grant expires.</summary>
    public TimeSpan RenewBefore { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Audio loss (thousandths) a receiver must report before its audio is sent with redundancy.</summary>
    public int RedundancyOnLossPermille { get; init; } = 15;

    /// <summary>Redundancy stops once reported loss stays below this many thousandths for <see cref="RedundancyHoldSeconds"/>.</summary>
    public int RedundancyOffLossPermille { get; init; } = 5;

    public int RedundancyHoldSeconds { get; init; } = 10;
}
