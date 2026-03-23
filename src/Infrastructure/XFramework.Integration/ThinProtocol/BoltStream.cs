using System.Net;
using System.Threading.Channels;
using StreamFlow.Domain.Shared.Buffers;
using StreamFlow.Domain.Shared.Protocol;

namespace XFramework.Integration.ThinProtocol;

/// <summary>
/// A bidirectional byte stream over the Bolt protocol.
/// Supports streaming any binary data: video, audio, files, sensor data, etc.
///
/// Usage (sender):
///   var stream = await client.OpenStreamAsync(recipientId, "video-feed");
///   await stream.SendAsync(videoFrame1);
///   await stream.SendAsync(videoFrame2);
///   await stream.CloseAsync();
///
/// Usage (receiver — register handler):
///   client.RegisterStreamHandler("video-feed", async (stream) => {
///       await foreach (var chunk in stream.ReadAllAsync())
///           ProcessVideoFrame(chunk);
///   });
/// </summary>
public sealed class BoltStream : IAsyncDisposable
{
    private readonly Guid _streamId;
    private readonly BoltConnection _connection;
    private readonly Channel<ReadOnlyMemory<byte>> _inboundChannel;
    private volatile bool _closed;

    public Guid StreamId => _streamId;
    public bool IsClosed => _closed;

    internal BoltStream(Guid streamId, BoltConnection connection)
    {
        _streamId = streamId;
        _connection = connection;
        _inboundChannel = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(1024)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });
    }

    /// <summary>
    /// Send a data chunk on this stream. Can be called repeatedly for continuous streaming.
    /// </summary>
    public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_closed) throw new InvalidOperationException("Stream is closed");

        var writer = RentedBufferWriter.GetThreadLocal();
        StreamFlowCodec.WriteStreamData(writer, _streamId, data.Span);
        await _connection.SendAsync(writer.WrittenMemory, ct);
    }

    /// <summary>
    /// Read all incoming chunks as an async enumerable.
    /// Completes when the remote side closes the stream.
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

    /// <summary>
    /// Close this stream gracefully.
    /// </summary>
    public async ValueTask CloseAsync(HttpStatusCode statusCode = HttpStatusCode.OK, CancellationToken ct = default)
    {
        if (_closed) return;
        _closed = true;

        var writer = RentedBufferWriter.GetThreadLocal();
        StreamFlowCodec.WriteStreamClose(writer, _streamId, statusCode);
        await _connection.SendAsync(writer.WrittenMemory, ct);

        _inboundChannel.Writer.TryComplete();
    }

    /// <summary>
    /// Called internally when a StreamData frame arrives for this stream.
    /// </summary>
    internal bool EnqueueInbound(ReadOnlyMemory<byte> data)
    {
        return _inboundChannel.Writer.TryWrite(data);
    }

    /// <summary>
    /// Called internally when a StreamClose frame arrives.
    /// </summary>
    internal void MarkClosed(HttpStatusCode statusCode)
    {
        _closed = true;
        _inboundChannel.Writer.TryComplete();
    }

    public async ValueTask DisposeAsync()
    {
        if (!_closed)
            await CloseAsync();
    }
}
