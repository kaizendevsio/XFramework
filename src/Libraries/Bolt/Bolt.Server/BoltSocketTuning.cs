using System.Net.Sockets;

namespace Bolt.Server;

/// <summary>Per-socket tuning for real-time media connections.</summary>
public static class BoltSocketTuning
{
    private const int IpProtoTcp = 6;
    private const int TcpNotSentLowWatermark = 25; // TCP_NOTSENT_LOWAT, Linux 3.12+

    /// <summary>
    /// Keep at most <paramref name="bytes"/> of not-yet-sent data in the kernel for this socket
    /// (Linux <c>TCP_NOTSENT_LOWAT</c>). Without it the kernel send buffer autotunes to megabytes and
    /// silently holds tens of seconds of media for a slow receiver, where nothing can prioritize audio
    /// or drop stale video. With it, writes wait sooner and the backlog stays in the relay's own
    /// per-receiver queues, which do. Best effort: returns false where unsupported.
    /// </summary>
    public static bool TryLimitUnsentBytes(Socket? socket, int bytes)
    {
        if (socket is null || bytes <= 0 || !OperatingSystem.IsLinux() || socket.ProtocolType != ProtocolType.Tcp)
            return false;
        try
        {
            socket.SetRawSocketOption(IpProtoTcp, TcpNotSentLowWatermark, BitConverter.GetBytes(bytes));
            return true;
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Cap the kernel's send buffer for this socket (<c>SO_SNDBUF</c>; Linux doubles the value for bookkeeping),
    /// which bounds what TCP keeps in flight plus unsent. <see cref="TryLimitUnsentBytes"/> only bounds the unsent
    /// part: a congestion window grown on a fast link still fills a router queue that just shrank (a 4 Mbps link
    /// stepping down to 512 kbps) with seconds of media nobody can take back. With this cap that backlog is at most
    /// about <paramref name="bytes"/> twice over. It is also a throughput ceiling of roughly twice
    /// <paramref name="bytes"/> per round trip, so it only suits a relay that owns the phone's bottleneck leg
    /// (no proxy in between) and paths whose bandwidth-delay product fits. It disables send-buffer autotuning.
    /// Best effort: returns false where it cannot be applied.
    /// </summary>
    public static bool TryLimitSendBuffer(Socket? socket, int bytes)
    {
        if (socket is null || bytes <= 0 || socket.ProtocolType != ProtocolType.Tcp) return false;
        try
        {
            socket.SendBufferSize = bytes;
            return true;
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException or PlatformNotSupportedException)
        {
            return false;
        }
    }
}
