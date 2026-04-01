using System.Buffers;
using System.Net.Quic;
using System.Threading.Channels;
using Bolt.Protocol.Transport;

namespace Bolt.Client.Transport;

/// <summary>
/// IBoltConnection implementation over QUIC with stream-per-RPC design.
///
/// Each SendAsync opens a fresh bidirectional stream, writes the frame, and closes writes.
/// This leverages QUIC's native multiplexing — multiple concurrent sends create parallel
/// streams with zero contention (no locks, no shared state, no send queue needed).
///
/// Inbound messages arrive on their own streams, accepted by a background loop that reads
/// each stream to completion and queues the frame into a Channel for ReceiveAsync consumers.
///
/// No persistent primary stream, no length-prefix framing — stream boundary IS the message
/// boundary (the sender calls CompleteWrites() to signal end of frame).
///
/// Datagrams are supported via an optional delegate. .NET's System.Net.Quic does not yet
/// expose the QUIC datagram extension (RFC 9221); when it does, pass the delegate at construction.
/// </summary>
public sealed class QuicBoltConnection : IBoltConnection
{
    private readonly QuicConnection _connection;
    private readonly Channel<(byte[] Buffer, int Length)> _inboundFrames;
    private readonly Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? _datagramSend;
    private Task? _acceptLoop;
    private CancellationTokenSource? _acceptCts;

    // ReceiveAsync state: tracks partially consumed frame across calls
    private byte[]? _currentBuffer;
    private int _currentOffset;
    private int _currentLength;

    /// <param name="connection">An established QUIC connection.</param>
    /// <param name="datagramSend">Optional delegate for unreliable datagram sends (RFC 9221).
    /// Not yet available in System.Net.Quic; wire up when the runtime exposes the API.</param>
    public QuicBoltConnection(
        QuicConnection connection,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? datagramSend = null)
    {
        _connection = connection;
        _datagramSend = datagramSend;
        _inboundFrames = Channel.CreateBounded<(byte[], int)>(
            new BoundedChannelOptions(4096)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
    }

    public BoltTransport TransportType => BoltTransport.Quic;

    public bool SupportsDatagrams => _datagramSend is not null;

    public bool SupportsParallelSend => true;

    public bool IsConnected => !_connection.RemoteCertificate?.Equals(null) ?? _acceptLoop is not null;

    /// <summary>Start accepting inbound streams. Call once after connection is established.</summary>
    public void StartAcceptLoop(CancellationToken ct = default)
    {
        _acceptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _acceptLoop = Task.Run(async () =>
        {
            try
            {
                while (!_acceptCts.Token.IsCancellationRequested)
                {
                    var stream = await _connection.AcceptInboundStreamAsync(_acceptCts.Token);
                    _ = ReadStreamIntoChannelAsync(stream, _acceptCts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (QuicException) { } // Connection closed
            finally { _inboundFrames.Writer.TryComplete(); }
        }, _acceptCts.Token);
    }

    private async Task ReadStreamIntoChannelAsync(QuicStream stream, CancellationToken ct)
    {
        try
        {
            var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            var totalRead = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(totalRead), ct)) > 0)
            {
                totalRead += read;
                // Grow buffer if needed
                if (totalRead >= buffer.Length - 1024)
                {
                    var newBuf = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                    buffer.AsSpan(0, totalRead).CopyTo(newBuf);
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = newBuf;
                }
            }

            if (totalRead > 0)
                await _inboundFrames.Writer.WriteAsync((buffer, totalRead), ct);
            else
                ArrayPool<byte>.Shared.Return(buffer);
        }
        catch { /* Stream read error — frame lost, not fatal */ }
        finally { await stream.DisposeAsync(); }
    }

    public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        // Stream-per-send: each call opens its own unidirectional stream.
        // Unidirectional = write-only, no read side to abort on dispose.
        // No locks needed — QUIC handles stream multiplexing natively.
        var stream = await _connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, ct);
        try
        {
            await stream.WriteAsync(data, ct);
            stream.CompleteWrites();
        }
        finally { await stream.DisposeAsync(); }
    }

    public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        // Deliver from partially consumed frame
        if (_currentBuffer is not null)
        {
            var remaining = _currentLength - _currentOffset;
            var toCopy = Math.Min(remaining, buffer.Length);
            _currentBuffer.AsSpan(_currentOffset, toCopy).CopyTo(buffer.Span);
            _currentOffset += toCopy;
            if (_currentOffset >= _currentLength)
            {
                ArrayPool<byte>.Shared.Return(_currentBuffer);
                _currentBuffer = null;
                return (toCopy, true);
            }
            return (toCopy, false);
        }

        // Wait for next complete frame from accept loop
        if (await _inboundFrames.Reader.WaitToReadAsync(ct))
        {
            if (_inboundFrames.Reader.TryRead(out var frame))
            {
                var (buf, len) = frame;
                var toCopy = Math.Min(len, buffer.Length);
                buf.AsSpan(0, toCopy).CopyTo(buffer.Span);
                if (toCopy < len)
                {
                    // Frame larger than caller's buffer — store remainder
                    _currentBuffer = buf;
                    _currentOffset = toCopy;
                    _currentLength = len;
                    return (toCopy, false);
                }
                ArrayPool<byte>.Shared.Return(buf);
                return (toCopy, true);
            }
        }
        return (0, true); // Channel completed = connection closed
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
        _acceptCts?.Cancel();
        if (_acceptLoop is not null)
            try { await _acceptLoop; } catch { }
        await _connection.CloseAsync(0, ct);
    }

    public async ValueTask DisposeAsync()
    {
        _acceptCts?.Cancel();
        if (_currentBuffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_currentBuffer);
            _currentBuffer = null;
        }
        // Drain any remaining frames in the channel
        while (_inboundFrames.Reader.TryRead(out var frame))
            ArrayPool<byte>.Shared.Return(frame.Buffer);
        await _connection.DisposeAsync();
        _acceptCts?.Dispose();
    }

    // Backward compat — called by old code, now a no-op (accept loop replaces primary stream)
    public Task OpenPrimaryStreamAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task AcceptPrimaryStreamAsync(CancellationToken ct = default) => Task.CompletedTask;
}
