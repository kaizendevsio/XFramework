using Bolt.Protocol.Transport;

namespace Bolt.Media.Congestion;

/// <summary>What a transport already does for media, so the media layer does not do it twice.</summary>
public static class MediaTransportPolicy
{
    /// <summary>
    /// A WebSocket runs over TCP: every byte arrives, in order, or the connection fails. A gap in a media stream
    /// there is a frame a relay or sender dropped on purpose, usually because the link is congested, so NACK
    /// retransmission only adds load at the worst moment. Keyframe requests stay.
    /// </summary>
    public static bool IsReliable(BoltTransport transport) => transport == BoltTransport.WebSocket;
}
