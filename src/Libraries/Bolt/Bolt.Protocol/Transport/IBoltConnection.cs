namespace Bolt.Protocol.Transport;

/// <summary>
/// Transport abstraction for Bolt protocol communication.
/// Implementations: WebSocket, QUIC, WebTransport.
///
/// The interface mirrors WebSocket message semantics (send complete message,
/// receive chunks with EndOfMessage flag). QUIC and WebTransport implementations
/// use 4-byte length-prefixed framing internally to provide message boundaries
/// over their byte-oriented streams.
/// </summary>
public interface IBoltConnection : IAsyncDisposable
{
    /// <summary>Send a complete binary message reliably (ordered, guaranteed delivery).</summary>
    ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);

    /// <summary>
    /// Receive the next message chunk into the buffer.
    /// Returns (bytesRead, endOfMessage). When endOfMessage is false,
    /// caller must keep reading to assemble the full message.
    /// Returns (0, true) when the connection is closed.
    /// </summary>
    ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default);

    /// <summary>
    /// Send a fire-and-forget datagram (unreliable, unordered).
    /// Used for drop-eligible media frames. No-op on transports that don't support datagrams.
    /// </summary>
    ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);

    /// <summary>Whether this transport supports unreliable datagrams (QUIC/WebTransport only).</summary>
    bool SupportsDatagrams { get; }

    /// <summary>
    /// True if this transport supports parallel sends without external serialization.
    /// QUIC: each send opens its own stream (inherently parallel).
    /// WebSocket: requires serialized sends (single connection).
    /// When true, BoltConnection bypasses the Channel send queue for direct sends.
    /// </summary>
    bool SupportsParallelSend { get; }

    /// <summary>Connection is open and usable.</summary>
    bool IsConnected { get; }

    /// <summary>Which transport this connection uses.</summary>
    BoltTransport TransportType { get; }

    /// <summary>Graceful close.</summary>
    ValueTask CloseAsync(CancellationToken ct = default);
}
