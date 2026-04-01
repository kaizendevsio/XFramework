using System.Buffers;
using System.Buffers.Binary;
using System.Net.Quic;
using System.Threading.Channels;
using Bolt.Protocol.Transport;

namespace Bolt.Client.Transport;

/// <summary>
/// IBoltConnection implementation over QUIC with pooled persistent streams.
///
/// Opens N persistent bidirectional streams at connection time. Sends are distributed
/// across streams via round-robin with per-stream locks. Receives are read from all
/// streams in parallel, feeding into one inbound channel.
///
/// This gives parallel throughput without the per-RPC stream creation/destruction
/// overhead of stream-per-RPC (~20 kernel calls eliminated per RPC).
///
/// Length-prefixed framing: [4:messageLength (uint32 LE)] [payload]
/// </summary>
public sealed class QuicBoltConnection : IBoltConnection
{
    private readonly QuicConnection _connection;
    private readonly Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? _datagramSend;
    private readonly Channel<(byte[] Buffer, int Length)> _inboundFrames;

    // Stream pool: persistent bidirectional streams with per-stream write locks
    private QuicStream[]? _poolStreams;
    private SemaphoreSlim[]? _poolLocks;
    private int _poolSize;
    private int _roundRobin;

    // Accept loop for server-initiated inbound streams (before pool is ready, or extra streams)
    private CancellationTokenSource? _cts;
    private readonly List<Task> _backgroundTasks = [];

    // Receive state: partially consumed frame for ReceiveAsync
    private byte[]? _currentBuffer;
    private int _currentOffset;
    private int _currentLength;

    public QuicBoltConnection(QuicConnection connection,
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
    public bool IsConnected => _poolStreams is not null || _cts is not null;

    /// <summary>
    /// Open the persistent stream pool and start read loops.
    /// Call once after QUIC connection is established (client side).
    /// </summary>
    public async Task StartStreamPoolAsync(int poolSize = 4, CancellationToken ct = default)
    {
        _cts = new CancellationTokenSource();
        _poolSize = poolSize;
        _poolStreams = new QuicStream[poolSize];
        _poolLocks = new SemaphoreSlim[poolSize];

        for (int i = 0; i < poolSize; i++)
        {
            _poolStreams[i] = await _connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, ct);
            _poolLocks[i] = new SemaphoreSlim(1, 1);
            // Start reading responses from this bidirectional stream
            _backgroundTasks.Add(Task.Run(() => ReadStreamLoopAsync(_poolStreams[i], _cts.Token)));
        }

        // Also accept any server-initiated inbound streams
        _backgroundTasks.Add(Task.Run(() => AcceptLoopAsync(_cts.Token)));
    }

    /// <summary>
    /// Accept persistent streams from the remote side (server-side usage).
    /// The client opens N bidirectional streams; the server accepts and reads from them.
    /// </summary>
    public async Task AcceptStreamPoolAsync(int poolSize = 4, CancellationToken ct = default)
    {
        _cts = new CancellationTokenSource();
        _poolSize = poolSize;
        _poolStreams = new QuicStream[poolSize];
        _poolLocks = new SemaphoreSlim[poolSize];

        for (int i = 0; i < poolSize; i++)
        {
            _poolStreams[i] = await _connection.AcceptInboundStreamAsync(ct);
            _poolLocks[i] = new SemaphoreSlim(1, 1);
            // Start reading from this stream
            _backgroundTasks.Add(Task.Run(() => ReadStreamLoopAsync(_poolStreams[i], _cts.Token)));
        }
    }

    public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_poolStreams is null || _poolLocks is null)
            throw new InvalidOperationException("Stream pool not started");

        // Round-robin across pooled streams
        var idx = (uint)Interlocked.Increment(ref _roundRobin) % _poolSize;
        var stream = _poolStreams[idx];
        var streamLock = _poolLocks[idx];

        // Write length-prefixed message under per-stream lock
        await streamLock.WaitAsync(ct);
        try
        {
            var totalSize = 4 + data.Length;
            var buf = ArrayPool<byte>.Shared.Rent(totalSize);
            try
            {
                BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)data.Length);
                data.Span.CopyTo(buf.AsSpan(4));
                await stream.WriteAsync(buf.AsMemory(0, totalSize), ct);
            }
            finally { ArrayPool<byte>.Shared.Return(buf); }
        }
        finally { streamLock.Release(); }
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

        // Wait for next complete frame from any read loop
        if (await _inboundFrames.Reader.WaitToReadAsync(ct))
        {
            if (_inboundFrames.Reader.TryRead(out var frame))
            {
                var (buf, len) = frame;
                var toCopy = Math.Min(len, buffer.Length);
                buf.AsSpan(0, toCopy).CopyTo(buffer.Span);
                if (toCopy < len)
                {
                    _currentBuffer = buf;
                    _currentOffset = toCopy;
                    _currentLength = len;
                    return (toCopy, false);
                }
                ArrayPool<byte>.Shared.Return(buf);
                return (toCopy, true);
            }
        }
        return (0, true);
    }

    public async ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_datagramSend is not null)
        {
            try { await _datagramSend(data, ct); }
            catch { }
        }
    }

    public async ValueTask CloseAsync(CancellationToken ct = default)
    {
        _cts?.Cancel();
        foreach (var task in _backgroundTasks)
            try { await task; } catch { }
        if (_poolStreams is not null)
            foreach (var stream in _poolStreams)
                try { stream.CompleteWrites(); await stream.DisposeAsync(); } catch { }
        await _connection.CloseAsync(0, ct);
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        if (_currentBuffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_currentBuffer);
            _currentBuffer = null;
        }
        while (_inboundFrames.Reader.TryRead(out var frame))
            ArrayPool<byte>.Shared.Return(frame.Buffer);
        if (_poolStreams is not null)
            foreach (var stream in _poolStreams)
                try { await stream.DisposeAsync(); } catch { }
        if (_poolLocks is not null)
            foreach (var lk in _poolLocks)
                lk.Dispose();
        await _connection.DisposeAsync();
        _cts?.Dispose();
    }

    // -- Background loops --

    /// <summary>Read length-prefixed messages from a persistent stream into the inbound channel.</summary>
    private async Task ReadStreamLoopAsync(QuicStream stream, CancellationToken ct)
    {
        var lengthBuf = new byte[4];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Read 4-byte length prefix
                var prefixRead = await ReadExactlyOrEofAsync(stream, lengthBuf.AsMemory(), ct);
                if (prefixRead == 0) break;

                var messageLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(lengthBuf);
                if (messageLength <= 0) continue;

                // Read full message into pooled buffer
                var msgBuf = ArrayPool<byte>.Shared.Rent(messageLength);
                var read = await ReadExactlyOrEofAsync(stream, msgBuf.AsMemory(0, messageLength), ct);
                if (read == 0) { ArrayPool<byte>.Shared.Return(msgBuf); break; }

                await _inboundFrames.Writer.WriteAsync((msgBuf, messageLength), ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (QuicException) { } // Stream/connection closed
    }

    /// <summary>Accept any additional inbound streams from the remote (server-initiated).</summary>
    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var stream = await _connection.AcceptInboundStreamAsync(ct);
                _backgroundTasks.Add(Task.Run(() => ReadStreamLoopAsync(stream, ct)));
            }
        }
        catch (OperationCanceledException) { }
        catch (QuicException) { }
        finally { _inboundFrames.Writer.TryComplete(); }
    }

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

    // Backward compat stubs (used by old code, redirect to new methods)
    public Task OpenPrimaryStreamAsync(CancellationToken ct = default) => StartStreamPoolAsync(ct: ct);
    public Task AcceptPrimaryStreamAsync(CancellationToken ct = default) => AcceptStreamPoolAsync(ct: ct);
    public void StartAcceptLoop(CancellationToken ct = default) => _ = StartStreamPoolAsync(ct: ct);
}
