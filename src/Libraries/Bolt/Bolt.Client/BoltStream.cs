using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using MemoryPack;
using Bolt.Protocol.Buffers;
using Bolt.Protocol;

namespace Bolt.Client;

/// <summary>
/// A bidirectional byte stream over the Bolt protocol.
/// Supports streaming any binary data: video, audio, files, sensor data, etc.
/// Also supports typed streaming via IAsyncEnumerable with MemoryPack serialization.
///
/// Raw bytes:
///   await stream.SendAsync(rawBytes);
///   await foreach (var chunk in stream.ReadAllAsync()) { ... }
///
/// Typed (auto-serialized):
///   await stream.SendAsync(myObject);
///   await foreach (var item in stream.ReadAllAsync&lt;MyType&gt;()) { ... }
///
/// IAsyncEnumerable pipe (send):
///   await stream.SendAllAsync(GetFramesAsync());
/// </summary>
public sealed class BoltStream : IAsyncDisposable
{
    internal delegate void InboundSink(ReadOnlySpan<byte> data);

    private readonly Guid _streamId;
    private readonly BoltConnection _connection;
    private readonly Channel<ReadOnlyMemory<byte>> _inboundChannel;
    private readonly Action<Guid>? _onClosed;
    private readonly object _inboundGate = new();
    private InboundSink? _inboundSink;
    private volatile bool _closed;

    public Guid StreamId => _streamId;
    public bool IsClosed => _closed;
    public HttpStatusCode? CloseStatus { get; private set; }
    internal BoltConnection Connection => _connection;

    internal BoltStream(
        Guid streamId,
        BoltConnection connection,
        Action<Guid>? onClosed = null,
        int inboundCapacity = 1024)
    {
        _streamId = streamId;
        _connection = connection;
        _onClosed = onClosed;
        _inboundChannel = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(Math.Max(1, inboundCapacity))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });
    }

    // ── Raw byte streaming ──

    /// <summary>
    /// Send raw bytes on this stream.
    /// </summary>
    public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_closed) throw new InvalidOperationException("Stream is closed");

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteStreamData(writer, _streamId, data.Span);
        await _connection.SendReliableAsync(writer, ct);
    }

    internal ValueTask<PooledSendCompletion> EnqueueAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken ct)
    {
        if (_closed) throw new InvalidOperationException("Stream is closed");

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteStreamData(writer, _streamId, data.Span);
        return _connection.EnqueueReliableAsync(writer, ct);
    }

    /// <summary>
    /// Send raw bytes on this stream.
    /// </summary>
    public ValueTask SendAsync(Memory<byte> data, CancellationToken ct = default)
        => SendAsync((ReadOnlyMemory<byte>)data, ct);

    /// <summary>
    /// Send raw bytes on this stream.
    /// </summary>
    public ValueTask SendAsync(byte[] data, CancellationToken ct = default)
        => SendAsync((ReadOnlyMemory<byte>)data, ct);

    /// <summary>
    /// Read all incoming raw byte chunks. Completes when remote closes the stream.
    /// </summary>
    public IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAllAsync(CancellationToken ct = default) =>
        _inboundChannel.Reader.ReadAllAsync(ct);

    /// <summary>
    /// Read a single chunk. Returns false when stream is closed.
    /// </summary>
    public async ValueTask<(bool HasData, ReadOnlyMemory<byte> Data)> ReadAsync(CancellationToken ct = default)
    {
        if (await _inboundChannel.Reader.WaitToReadAsync(ct))
        {
            if (_inboundChannel.Reader.TryRead(out var data))
                return (true, data);
        }
        return (false, ReadOnlyMemory<byte>.Empty);
    }

    // ── Typed streaming (MemoryPack auto-serialization) ──

    /// <summary>
    /// Send a typed object — auto-serialized with MemoryPack into a pooled buffer.
    /// </summary>
    public async ValueTask SendAsync<T>(T item, CancellationToken ct = default)
    {
        var serWriter = new RentedBufferWriter(256);
        try
        {
            MemoryPackSerializer.Serialize(serWriter, item);
            await SendAsync(serWriter.WrittenMemory, ct);
        }
        finally { serWriter.Dispose(); }
    }

    /// <summary>
    /// Pipe an entire IAsyncEnumerable into the stream.
    /// Each item is serialized with MemoryPack and sent as a StreamData frame.
    /// Closes the stream when the enumerable completes.
    /// </summary>
    public async Task SendAllAsync<T>(IAsyncEnumerable<T> items, CancellationToken ct = default)
    {
        var serWriter = new RentedBufferWriter(256);
        try
        {
            await foreach (var item in items.WithCancellation(ct))
            {
                serWriter.Reset();
                MemoryPackSerializer.Serialize(serWriter, item);
                await SendAsync(serWriter.WrittenMemory, ct);
            }
        }
        finally { serWriter.Dispose(); }
        await CloseAsync(ct: ct);
    }

    /// <summary>
    /// Read all incoming chunks as typed objects — auto-deserialized with MemoryPack.
    /// Completes when the remote side closes the stream.
    /// </summary>
    public async IAsyncEnumerable<T> ReadAllAsync<T>([EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var chunk in _inboundChannel.Reader.ReadAllAsync(ct))
        {
            var item = MemoryPackSerializer.Deserialize<T>(chunk.Span);
            if (item is not null)
                yield return item;
        }
    }

    // ── Lifecycle ──

    /// <summary>
    /// Close this stream gracefully.
    /// </summary>
    public async ValueTask CloseAsync(HttpStatusCode statusCode = HttpStatusCode.OK, CancellationToken ct = default)
    {
        lock (_inboundGate)
        {
            if (_closed) return;
            _closed = true;
            CloseStatus = statusCode;
            _inboundSink = null;
        }

        try
        {
            var writer = RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteStreamClose(writer, _streamId, statusCode);
            await _connection.SendReliableAsync(writer, ct);
        }
        finally
        {
            lock (_inboundGate)
                _inboundChannel.Writer.TryComplete();
            _onClosed?.Invoke(_streamId);
        }
    }

    internal bool TryEnqueueInbound(ReadOnlyMemory<byte> data)
    {
        lock (_inboundGate)
        {
            if (!_closed && _inboundSink is { } sink)
            {
                sink(data.Span);
                return true;
            }

            if (!_closed && _inboundChannel.Writer.TryWrite(data))
                return true;
        }

        return FailInboundCapacity();
    }

    internal bool TryAcceptInbound(ReadOnlySpan<byte> data)
    {
        lock (_inboundGate)
        {
            if (_closed)
                return false;

            if (_inboundSink is { } sink)
            {
                sink(data);
                return true;
            }

            var copy = GC.AllocateUninitializedArray<byte>(data.Length);
            data.CopyTo(copy);
            if (_inboundChannel.Writer.TryWrite(copy))
                return true;
        }

        return FailInboundCapacity();
    }

    internal bool TrySetInboundSink(InboundSink sink)
    {
        lock (_inboundGate)
        {
            if (_inboundSink is not null)
                return false;

            while (_inboundChannel.Reader.TryRead(out var queued))
                sink(queued.Span);

            if (!_closed)
                _inboundSink = sink;
            return true;
        }
    }

    internal void ClearInboundSink(InboundSink sink)
    {
        lock (_inboundGate)
        {
            if (_inboundSink == sink)
                _inboundSink = null;
        }
    }

    internal Task WaitForCloseAsync(CancellationToken ct) =>
        _inboundChannel.Reader.Completion.WaitAsync(ct);

    private bool FailInboundCapacity()
    {
        lock (_inboundGate)
        {
            if (_closed)
                return false;

            _closed = true;
            CloseStatus = HttpStatusCode.TooManyRequests;
            _inboundSink = null;
            _inboundChannel.Writer.TryComplete(new InvalidOperationException(
                $"Bolt stream {_streamId} exceeded its inbound buffer capacity."));
        }
        _onClosed?.Invoke(_streamId);
        return false;
    }

    internal void MarkClosed(HttpStatusCode statusCode)
    {
        lock (_inboundGate)
        {
            _closed = true;
            CloseStatus = statusCode;
            _inboundSink = null;
            _inboundChannel.Writer.TryComplete();
        }
        _onClosed?.Invoke(_streamId);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_closed)
            await CloseAsync();
    }
}
