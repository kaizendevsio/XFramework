namespace Bolt.Protocol.Transport;

/// <summary>Available transport protocols for Bolt connections.</summary>
public enum BoltTransport
{
    /// <summary>QUIC datagrams for media frames only. Not used for RPC transport.</summary>
    Quic,
    /// <summary>HTTP/3 WebTransport. Default for browsers (Chrome/Edge).</summary>
    WebTransport,
    /// <summary>WebSocket over HTTP/1.1. Universal fallback.</summary>
    WebSocket
}
