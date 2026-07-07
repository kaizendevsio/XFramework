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
    private readonly Guid _streamId;
    private readonly BoltConnection _connection;
    private readonly Channel<ReadOnlyMemory<byte>> _inboundChannel;
    private readonly Action<Guid>? _onClosed;
    private volatile bool _closed;

    public Guid StreamId => _streamId;
    public bool IsClosed => _closed;

    internal BoltStream(Guid streamId, BoltConnection connection, Action<Guid>? onClosed = null)
    {
        _streamId = streamId;
        _connection = connection;
        _onClosed = onClosed;
        _inboundChannel = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(1024)
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
        await _connection.SendAsync(writer.WrittenMemory, ct);
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
    public IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAllAsync(CancellationToken ct = default)
    {
        return _inboundChannel.Reader.ReadAllAsync(ct);
    }

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
        if (_closed) return;
        _closed = true;

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteStreamClose(writer, _streamId, statusCode);
        await _connection.SendAsync(writer.WrittenMemory, ct);

        _inboundChannel.Writer.TryComplete();
        _onClosed?.Invoke(_streamId);
    }

    internal ValueTask EnqueueInboundAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
        => _inboundChannel.Writer.WriteAsync(data, ct);

    internal void MarkClosed(HttpStatusCode statusCode)
    {
        _closed = true;
        _inboundChannel.Writer.TryComplete();
        _onClosed?.Invoke(_streamId);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_closed)
            await CloseAsync();
    }
}
