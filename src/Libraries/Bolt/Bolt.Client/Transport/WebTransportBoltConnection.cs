using System.Buffers;
using System.Buffers.Binary;
using Bolt.Protocol.Transport;

namespace Bolt.Client.Transport;

/// <summary>
/// IBoltConnection implementation over WebTransport (HTTP/3).
/// Uses same 4-byte length-prefixed framing as QuicBoltConnection.
///
/// WebTransport provides both reliable streams and unreliable datagrams,
/// accessible from browsers (Chrome/Edge) via the WebTransport API.
///
/// Server-side: wraps a bidirectional WebTransport stream.
/// Browser-side: would wrap JS WebTransport API via interop (future).
/// </summary>
public sealed class WebTransportBoltConnection : IBoltConnection
{
    private readonly Stream _stream;
    private readonly Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? _datagramSend;
    private readonly Func<ValueTask>? _sessionClose;
    private volatile bool _closed;

    // Receive state
    private int _remainingMessageBytes;
    private readonly byte[] _lengthBuf = new byte[4];

    /// <summary>
    /// Create from a bidirectional WebTransport stream.
    /// </summary>
    /// <param name="stream">The bidirectional stream for reliable communication.</param>
    /// <param name="datagramSend">Optional delegate to send unreliable datagrams.</param>
    /// <param name="sessionClose">Optional delegate to close the WebTransport session.</param>
    public WebTransportBoltConnection(
        Stream stream,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? datagramSend = null,
        Func<ValueTask>? sessionClose = null)
    {
        _stream = stream;
        _datagramSend = datagramSend;
        _sessionClose = sessionClose;
    }

    public BoltTransport TransportType => BoltTransport.WebTransport;

    public bool SupportsDatagrams => _datagramSend is not null;

    public bool IsConnected => !_closed && _stream.CanRead && _stream.CanWrite;

    public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        // No lock needed — BoltConnection.SendAsync already serializes.
        var totalSize = 4 + data.Length;
        var buf = ArrayPool<byte>.Shared.Rent(totalSize);
        try
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)data.Length);
            data.Span.CopyTo(buf.AsSpan(4));
            await _stream.WriteAsync(buf.AsMemory(0, totalSize), ct);
            await _stream.FlushAsync(ct);
        }
        finally { ArrayPool<byte>.Shared.Return(buf); }
    }

    public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (_remainingMessageBytes > 0)
        {
            var toRead = Math.Min(_remainingMessageBytes, buffer.Length);
            var bytesRead = await ReadExactlyOrEofAsync(_stream, buffer[..toRead], ct);
            if (bytesRead == 0) return (0, true);
            _remainingMessageBytes -= bytesRead;
            return (bytesRead, _remainingMessageBytes == 0);
        }

        var prefixRead = await ReadExactlyOrEofAsync(_stream, _lengthBuf.AsMemory(), ct);
        if (prefixRead == 0) return (0, true);

        var messageLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(_lengthBuf);
        if (messageLength == 0) return (0, false);

        var chunkSize = Math.Min(messageLength, buffer.Length);
        var read = await ReadExactlyOrEofAsync(_stream, buffer[..chunkSize], ct);
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
        _closed = true;
        _stream.Close();
        if (_sessionClose is not null)
            await _sessionClose();
    }

    public async ValueTask DisposeAsync()
    {
        _closed = true;
        await _stream.DisposeAsync();
    }

    private static async Task<int> ReadExactlyOrEofAsync(Stream stream, Memory<byte> buffer, CancellationToken ct)
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
