using System.Buffers;
using System.Buffers.Binary;
using System.Net.Quic;
using Bolt.Protocol.Transport;

namespace Bolt.Client.Transport;

/// <summary>
/// IBoltConnection implementation over QUIC.
/// Uses a single persistent bidirectional stream with 4-byte length-prefixed framing
/// to provide message boundaries over QUIC's byte-oriented streams.
///
/// Wire format per message: [4:messageLength (uint32 LE)] [messageLength bytes of Bolt frame]
///
/// Datagrams are supported via an optional delegate. .NET's System.Net.Quic does not yet
/// expose the QUIC datagram extension (RFC 9221); when it does, pass the delegate at construction.
/// </summary>
public sealed class QuicBoltConnection : IBoltConnection
{
    private readonly QuicConnection _connection;
    private readonly Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? _datagramSend;
    private QuicStream? _primaryStream;
    // Receive state: tracks partially read messages across ReceiveAsync calls
    private int _remainingMessageBytes;
    private readonly byte[] _lengthBuf = new byte[4];

    /// <param name="connection">An established QUIC connection.</param>
    /// <param name="datagramSend">Optional delegate for unreliable datagram sends (RFC 9221).
    /// Not yet available in System.Net.Quic; wire up when the runtime exposes the API.</param>
    public QuicBoltConnection(
        QuicConnection connection,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? datagramSend = null)
    {
        _connection = connection;
        _datagramSend = datagramSend;
    }

    public BoltTransport TransportType => BoltTransport.Quic;

    public bool SupportsDatagrams => _datagramSend is not null;

    public bool IsConnected => _primaryStream is not null;

    /// <summary>Open the primary bidirectional stream. Called once after QUIC connection is established.</summary>
    public async Task OpenPrimaryStreamAsync(CancellationToken ct = default)
    {
        _primaryStream = await _connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, ct);
    }

    /// <summary>Accept the primary stream from the remote side (server-side usage).</summary>
    public async Task AcceptPrimaryStreamAsync(CancellationToken ct = default)
    {
        _primaryStream = await _connection.AcceptInboundStreamAsync(ct);
    }

    public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_primaryStream is null) throw new InvalidOperationException("Primary stream not opened");

        // No lock needed — BoltConnection.SendAsync already serializes sends per connection.
        var totalSize = 4 + data.Length;
        var buf = ArrayPool<byte>.Shared.Rent(totalSize);
        try
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)data.Length);
            data.Span.CopyTo(buf.AsSpan(4));
            await _primaryStream.WriteAsync(buf.AsMemory(0, totalSize), ct);
        }
        finally { ArrayPool<byte>.Shared.Return(buf); }
    }

    public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (_primaryStream is null) throw new InvalidOperationException("Primary stream not opened");

        // If we're in the middle of reading a message, continue reading payload bytes
        if (_remainingMessageBytes > 0)
        {
            var toRead = Math.Min(_remainingMessageBytes, buffer.Length);
            var bytesRead = await ReadExactlyOrEofAsync(_primaryStream, buffer[..toRead], ct);
            if (bytesRead == 0) return (0, true);
            _remainingMessageBytes -= bytesRead;
            return (bytesRead, _remainingMessageBytes == 0);
        }

        // Read 4-byte length prefix for the next message
        var prefixRead = await ReadExactlyOrEofAsync(_primaryStream, _lengthBuf.AsMemory(), ct);
        if (prefixRead == 0) return (0, true);

        var messageLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(_lengthBuf);
        if (messageLength == 0) return (0, false);

        // Read as much of the payload as fits in the caller's buffer
        var chunkSize = Math.Min(messageLength, buffer.Length);
        var read = await ReadExactlyOrEofAsync(_primaryStream, buffer[..chunkSize], ct);
        if (read == 0) return (0, true);

        _remainingMessageBytes = messageLength - read;
        return (read, _remainingMessageBytes == 0);
    }

    public async ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_datagramSend is not null)
        {
            try { await _datagramSend(data, ct); }
            catch { /* Unreliable -- failures silently ignored */ }
        }
    }

    public async ValueTask CloseAsync(CancellationToken ct = default)
    {
        if (_primaryStream is not null)
        {
            _primaryStream.CompleteWrites();
            await _primaryStream.DisposeAsync();
            _primaryStream = null;
        }
        await _connection.CloseAsync(0, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_primaryStream is not null)
            await _primaryStream.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>Read exactly the requested bytes, or return 0 if stream is closed.</summary>
    private static async Task<int> ReadExactlyOrEofAsync(QuicStream stream, Memory<byte> buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], ct);
            if (read == 0) return totalRead == 0 ? 0 : totalRead;
            totalRead += read;
        }
        return totalRead;
    }
}
