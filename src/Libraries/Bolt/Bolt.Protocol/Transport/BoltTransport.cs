namespace Bolt.Protocol.Transport;

/// <summary>Available transport protocols for Bolt connections.</summary>
public enum BoltTransport
{
    /// <summary>Raw QUIC with ALPN "bolt". Default for .NET server-to-server.</summary>
    Quic,
    /// <summary>HTTP/3 WebTransport. Default for browsers (Chrome/Edge).</summary>
    WebTransport,
    /// <summary>WebSocket over HTTP/1.1. Universal fallback.</summary>
    WebSocket
}
