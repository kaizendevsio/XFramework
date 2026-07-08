using System.Buffers;
using System.Buffers.Binary;
using Bolt.Protocol.Transport;

namespace Bolt.Client.Transport;

/// <summary>
/// IBoltConnection implementation over WebTransport (HTTP/3).
/// Uses 4-byte length-prefixed framing over byte-oriented streams.
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
            if (!await TryReadExactlyAsync(_stream, buffer[..toRead], ct))
            {
                _remainingMessageBytes = 0;
                _closed = true;
                return (0, true);
            }

            _remainingMessageBytes -= toRead;
            return (toRead, _remainingMessageBytes == 0);
        }

        if (!await TryReadExactlyAsync(_stream, _lengthBuf.AsMemory(), ct))
        {
            _closed = true;
            return (0, true);
        }

        var messageLength = BinaryPrimitives.ReadUInt32LittleEndian(_lengthBuf);
        if (messageLength == 0 || messageLength > int.MaxValue)
        {
            _closed = true;
            return (0, true);
        }

        var chunkSize = Math.Min((int)messageLength, buffer.Length);
        if (!await TryReadExactlyAsync(_stream, buffer[..chunkSize], ct))
        {
            _closed = true;
            return (0, true);
        }

        _remainingMessageBytes = (int)messageLength - chunkSize;
        return (chunkSize, _remainingMessageBytes == 0);
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

    private static async Task<bool> TryReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], ct);
            if (read == 0) return false;
            totalRead += read;
        }

        return true;
    }
}
