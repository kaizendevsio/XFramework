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
}
