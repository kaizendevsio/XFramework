using System.Buffers.Binary;
using System.Threading.Channels;
using Bolt.Client.Media;
using Bolt.Protocol;
using Bolt.Protocol.Buffers;

namespace Bolt.Client;

/// <summary>
/// Decoded media frame data delivered to consumers.
/// </summary>
public readonly record struct MediaFrameData(uint SequenceNumber, uint Timestamp, ReadOnlyMemory<byte> Data, bool IsKeyframe);

/// <summary>
/// A media-specific stream for sending/receiving encoded audio/video frames.
/// Unlike <see cref="BoltStream"/> (general-purpose byte streaming), this is optimized
/// for real-time media: sequence numbers, timestamps, keyframe flags, and drop-oldest
/// back-pressure to keep latency bounded.
/// </summary>
public sealed class BoltMediaStream : IAsyncDisposable
{
    private readonly BoltConnection _connection;
    private readonly Channel<MediaFrameData> _inbound;
    private uint _nextSequence;
    private uint _timestampCounter;
    private readonly uint _timestampIncrement; // 960 for Opus 48kHz/20ms, 3000 for 30fps video at 90kHz
    private bool _closed;
    private FecEncoder? _fecEncoder;
    private FecDecoder? _fecDecoder;
    private uint _lastReceivedSeq;

    /// <summary>Unique identifier for this media stream.</summary>
    public Guid StreamId { get; }

    /// <summary>The call this media stream belongs to.</summary>
    public Guid CallId { get; }

    /// <summary>True if this is an audio stream; false for video.</summary>
    public bool IsAudio { get; }

    internal BoltMediaStream(BoltConnection connection, Guid streamId, Guid callId, bool isAudio)
    {
        _connection = connection;
        StreamId = streamId;
        CallId = callId;
        IsAudio = isAudio;
        _timestampIncrement = isAudio ? 960u : 3000u; // Opus 48kHz/20ms or 30fps at 90kHz
        _inbound = Channel.CreateBounded<MediaFrameData>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    /// <summary>
    /// Enable Forward Error Correction for this stream.
    /// FEC encodes parity across groups of frames to recover single-frame losses.
    /// </summary>
    public void EnableFec(int groupSize = 4)
    {
        _fecEncoder = new FecEncoder(groupSize);
        _fecDecoder = new FecDecoder();
    }

    /// <summary>
    /// Called internally by the receive loop when an FEC parity frame arrives.
    /// The payload starts with groupSize * 4 bytes of original frame lengths (int32 LE),
    /// followed by the XOR parity data.
    /// </summary>
    internal void EnqueueFecFrame(uint groupStart, byte groupSize, ReadOnlyMemory<byte> payload)
    {
        if (_fecDecoder == null) return;
        var span = payload.Span;
        var lengths = new int[groupSize];
        for (int i = 0; i < groupSize; i++)
            lengths[i] = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(i * 4));
        var parityData = payload.Slice(groupSize * 4);
        _fecDecoder.AddFecFrame(groupStart, groupSize, parityData, lengths);
    }

    /// <summary>
    /// Send an encoded media frame (audio or video) to the remote peer.
    /// Sequence numbers and timestamps are auto-incremented.
    /// If FEC is enabled, parity frames are sent after each group completes.
    /// </summary>
    public async ValueTask SendFrameAsync(ReadOnlyMemory<byte> encodedData, bool isKeyframe = false, CancellationToken ct = default)
    {
        if (_closed) return;

        var seq = _nextSequence++;
        var ts = _timestampCounter;
        _timestampCounter += _timestampIncrement;

        byte flags = 0;
        if (isKeyframe) flags |= 0x01;

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteMediaFrame(writer, StreamId, seq, ts, flags, encodedData.Span);
        await _connection.SendAsync(writer.WrittenMemory, ct);
        writer.Reset();

        if (_fecEncoder != null)
        {
            var fecResult = _fecEncoder.AddFrame(seq, encodedData);
            if (fecResult != null)
            {
                // Serialize original frame lengths before parity data so the decoder
                // can truncate recovered frames to the correct size.
                var lengthBytes = new byte[fecResult.OriginalLengths.Length * 4];
                for (int i = 0; i < fecResult.OriginalLengths.Length; i++)
                    BinaryPrimitives.WriteInt32LittleEndian(lengthBytes.AsSpan(i * 4), fecResult.OriginalLengths[i]);
                var fecPayload = new byte[lengthBytes.Length + fecResult.ParityData.Length];
                lengthBytes.CopyTo(fecPayload, 0);
                fecResult.ParityData.CopyTo(fecPayload, lengthBytes.Length);

                var fecWriter = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteFecFrame(fecWriter, StreamId, fecResult.GroupStartSequence, fecResult.GroupSize, fecPayload);
                await _connection.SendAsync(fecWriter.WrittenMemory, ct);
                fecWriter.Reset();
            }
        }
    }

    /// <summary>
    /// Called internally by the receive loop to deliver an inbound frame.
    /// Data is copied since the receive buffer will be reused.
    /// </summary>
    internal void EnqueueFrame(uint seq, uint timestamp, ReadOnlyMemory<byte> data, byte flags)
    {
        var isKeyframe = (flags & 0x01) != 0;
        // Copy the data since the receive buffer will be reused
        var copy = new byte[data.Length];
        data.CopyTo(copy);
        _inbound.Writer.TryWrite(new MediaFrameData(seq, timestamp, copy, isKeyframe));

        // Register frame with FEC decoder for potential recovery
        _fecDecoder?.AddFrame(seq, seq, data);

        // Check for gaps and attempt FEC recovery for missing frames
        if (_fecDecoder != null && _lastReceivedSeq > 0)
        {
            for (uint missing = _lastReceivedSeq + 1; missing < seq; missing++)
            {
                if (_fecDecoder.TryRecover(missing, missing, out var recovered))
                {
                    _inbound.Writer.TryWrite(new MediaFrameData(missing, 0, recovered, false));
                }
            }
        }
        _lastReceivedSeq = seq;
    }

    /// <summary>
    /// Read all incoming media frames as an async stream.
    /// Completes when the media stream is disposed.
    /// </summary>
    public IAsyncEnumerable<MediaFrameData> ReadFramesAsync(CancellationToken ct = default)
        => _inbound.Reader.ReadAllAsync(ct);

    public async ValueTask DisposeAsync()
    {
        _closed = true;
        _inbound.Writer.TryComplete();
    }
}
