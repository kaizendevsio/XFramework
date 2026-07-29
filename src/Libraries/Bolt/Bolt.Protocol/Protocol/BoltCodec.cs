using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace Bolt.Protocol;

/// <summary>
/// Zero-allocation binary codec for the thin Bolt protocol.
///
/// Request frame:  [1:type] [16:requestId] [4:recipientHash] [4:senderHash] [4:commandHash] [4:payloadLen] [payload]
///                 = 33 bytes header + N payload
///
/// Response frame: [1:type] [16:requestId] [2:statusCode] [4:payloadLen] [payload]
///                 = 23 bytes header + N payload
///
/// Register frame: [1:type] [2:wireVersion] [4:clientIdLen] [clientId UTF-8] [4:clientNameLen] [clientName UTF-8]
///
/// All multi-byte values are little-endian.
/// </summary>
public static class BoltCodec
{
    public const ushort WireVersion = 2;
    public const int DefaultMaxFrameBytes = 8 * 1024 * 1024;
    public const int MaxBatchFrames = 32;
    public const int MaxBatchBytes = 256 * 1024;
    public const int BatchHeaderSize = 1 + 4;
    public const int RegisterAckSize = 1 + 1 + 2;
    private const int DefaultMaxStringBytes = 64 * 1024;

    public const int RequestHeaderSize = 1 + 16 + 4 + 4 + 4 + 4;   // 33 bytes (added senderHash)
    public const int ResponseHeaderSize = 1 + 16 + 2 + 4;       // 23 bytes
    public const int RequestCancelSize = 1 + 16;

    // Media frame header sizes
    public const int MediaFrameHeaderSize = 1 + 16 + 4 + 4 + 1 + 4;     // 30 bytes
    public const int MediaConfigHeaderSize = 1 + 16 + 16 + 1 + 1 + 4 + 4 + 4 + 1 + 4; // 52 bytes
    public const int MediaFeedbackSize = 1 + 16 + 4 + 4 + 4 + 2 + 1;    // 32 bytes
    public const int MediaKeyRequestSize = 1 + 16;                        // 17 bytes
    public const int CallSignalHeaderSize = 1 + 16 + 1 + 4;              // 22 bytes
    public const int FecFrameHeaderSize = 1 + 16 + 4 + 1 + 4;            // 26 bytes
    public const int NackRequestHeaderSize = 1 + 16 + 2;                  // 19 bytes (+ nackCount * 4)

    // Pub/sub header sizes (variable for Subscribe/Unsubscribe/Publish/Ack due to string fields)
    public const int PublishHeaderSize = 1 + 4 + 1 + 4 + 4;        // 14 bytes + topic + payload
    public const int EventHeaderSize = 1 + 4 + 8 + 1 + 4 + 4;      // 22 bytes + subscriberId + payload
    // Subscribe header is variable: 1 + 4 + 1 + 4 + N + 4 + M + 4 + T (18 + subscriberId + topic + actorToken)
    // Unsubscribe header is variable: 1 + 4 + 4 + N + 4 + M + 1 + 4 + T (18 + topic + subscriberId + actorToken)
    // Ack header is variable: 1 + 4 + 4 + N + 4 + M + 8 + 4 + T (25 + topic + subscriberId + actorToken)

    #region Encoding

    /// <summary>
    /// Encode a request frame: [1:type][16:requestId][4:recipientHash][4:senderHash][4:commandHash][4:payloadLen][payload]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteRequest(IBufferWriter<byte> writer, Guid requestId, int recipientHash, int senderHash, int commandHash, ReadOnlySpan<byte> payload)
    {
        var totalSize = ValidateFrameSize((long)RequestHeaderSize + payload.Length);
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.Request;
        WriteGuid(span.Slice(1), requestId);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(17), recipientHash);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(21), senderHash);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(25), commandHash);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(29), payload.Length);
        payload.CopyTo(span.Slice(33));

        writer.Advance(totalSize);
        return totalSize;
    }

    /// <summary>
    /// Encode a response frame into the provided buffer writer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteResponse(IBufferWriter<byte> writer, Guid requestId, HttpStatusCode statusCode, ReadOnlySpan<byte> payload)
    {
        var totalSize = ValidateFrameSize((long)ResponseHeaderSize + payload.Length);
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.Response;
        WriteGuid(span.Slice(1), requestId);
        BinaryPrimitives.WriteInt16LittleEndian(span.Slice(17), (short)statusCode);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(19), payload.Length);
        payload.CopyTo(span.Slice(23));

        writer.Advance(totalSize);
        return totalSize;
    }

    /// <summary>
    /// Encode a register frame. Variable-length.
    /// </summary>
    public static int WriteRegister(IBufferWriter<byte> writer, string clientId, string clientName)
    {
        var idBytes = Encoding.UTF8.GetByteCount(clientId);
        var nameBytes = Encoding.UTF8.GetByteCount(clientName);
        ValidateStringLength(idBytes, DefaultMaxStringBytes, nameof(clientId), allowEmpty: false);
        ValidateStringLength(nameBytes, DefaultMaxStringBytes, nameof(clientName), allowEmpty: false);
        var totalSize = ValidateFrameSize(11L + idBytes + nameBytes);
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.Register;
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(1), WireVersion);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(3), idBytes);
        Encoding.UTF8.GetBytes(clientId, span.Slice(7));
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(7 + idBytes), nameBytes);
        Encoding.UTF8.GetBytes(clientName, span.Slice(11 + idBytes));

        writer.Advance(totalSize);
        return totalSize;
    }

    /// <summary>
    /// Encode a register acknowledgement.
    /// </summary>
    public static int WriteRegisterAck(IBufferWriter<byte> writer, bool success)
    {
        var span = writer.GetSpan(RegisterAckSize);
        span[0] = (byte)FrameType.RegisterAck;
        span[1] = success ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(2), WireVersion);
        writer.Advance(RegisterAckSize);
        return RegisterAckSize;
    }

    public static int WriteBatch(
        IBufferWriter<byte> writer,
        IReadOnlyList<ReadOnlyMemory<byte>> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count is < 2 or > MaxBatchFrames)
            throw new ArgumentOutOfRangeException(nameof(frames), $"Bolt batches require 2 to {MaxBatchFrames} frames.");

        long totalSize = BatchHeaderSize;
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            if (!IsValidBatchInnerFrame(frame.Span))
                throw new ArgumentException("Bolt batches cannot contain empty, registration, nested batch, or media frames.", nameof(frames));
            totalSize += 4L + frame.Length;
        }

        if (totalSize > MaxBatchBytes)
            throw new ArgumentOutOfRangeException(nameof(frames), $"Bolt batches cannot exceed {MaxBatchBytes} bytes.");

        var size = (int)totalSize;
        var destination = writer.GetSpan(size);
        destination[0] = (byte)FrameType.Batch;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1), frames.Count);
        var offset = BatchHeaderSize;
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset), frame.Length);
            offset += 4;
            frame.Span.CopyTo(destination.Slice(offset));
            offset += frame.Length;
        }

        writer.Advance(size);
        return size;
    }

    public static int WriteBatch<T>(
        IBufferWriter<byte> writer,
        ReadOnlySpan<T> frames,
        Func<T, ReadOnlyMemory<byte>> getFrame)
    {
        ArgumentNullException.ThrowIfNull(getFrame);
        if (frames.Length is < 2 or > MaxBatchFrames)
            throw new ArgumentOutOfRangeException(nameof(frames), $"Bolt batches require 2 to {MaxBatchFrames} frames.");

        long totalSize = BatchHeaderSize;
        for (var i = 0; i < frames.Length; i++)
        {
            var frame = getFrame(frames[i]);
            if (!IsValidBatchInnerFrame(frame.Span))
                throw new ArgumentException("Bolt batches cannot contain empty, registration, nested batch, or media frames.", nameof(frames));
            totalSize += 4L + frame.Length;
        }

        if (totalSize > MaxBatchBytes)
            throw new ArgumentOutOfRangeException(nameof(frames), $"Bolt batches cannot exceed {MaxBatchBytes} bytes.");

        var size = (int)totalSize;
        var destination = writer.GetSpan(size);
        destination[0] = (byte)FrameType.Batch;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1), frames.Length);
        var offset = BatchHeaderSize;
        for (var i = 0; i < frames.Length; i++)
        {
            var frame = getFrame(frames[i]);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset), frame.Length);
            offset += 4;
            frame.Span.CopyTo(destination.Slice(offset));
            offset += frame.Length;
        }

        writer.Advance(size);
        return size;
    }

    /// <summary>
    /// Encode a push frame (fire-and-forget, same header as Request but type=0x05).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WritePush(IBufferWriter<byte> writer, Guid requestId, int recipientHash, int senderHash, int commandHash, ReadOnlySpan<byte> payload)
    {
        var totalSize = ValidateFrameSize((long)RequestHeaderSize + payload.Length);
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.Push;
        WriteGuid(span.Slice(1), requestId);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(17), recipientHash);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(21), senderHash);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(25), commandHash);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(29), payload.Length);
        payload.CopyTo(span.Slice(33));

        writer.Advance(totalSize);
        return totalSize;
    }

    /// <summary>
    /// Encode a Subscribe frame:
    /// [1:type=0x06] [4:topicHash] [1:flags] [4:subscriberIdLen] [subscriberId UTF-8]
    /// [4:topicLen] [topic UTF-8] [4:actorTokenLen] [actorToken UTF-8]
    /// </summary>
    public static int WriteSubscribe(
        IBufferWriter<byte> writer,
        string topic,
        string subscriberId,
        bool durable,
        string? actorAccessToken = null)
    {
        actorAccessToken ??= string.Empty;
        var topicBytes = Encoding.UTF8.GetByteCount(topic);
        var idBytes = Encoding.UTF8.GetByteCount(subscriberId);
        var tokenBytes = Encoding.UTF8.GetByteCount(actorAccessToken);
        ValidatePubSubStrings(topicBytes, idBytes, tokenBytes);
        var totalSize = ValidateFrameSize(18L + idBytes + topicBytes + tokenBytes);
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.Subscribe;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(1), Fnv1aHash(topic));
        span[5] = (byte)(durable ? 0x01 : 0x00);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(6), idBytes);
        Encoding.UTF8.GetBytes(subscriberId, span.Slice(10));
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(10 + idBytes), topicBytes);
        Encoding.UTF8.GetBytes(topic, span.Slice(14 + idBytes));
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(14 + idBytes + topicBytes), tokenBytes);
        Encoding.UTF8.GetBytes(actorAccessToken, span.Slice(18 + idBytes + topicBytes));

        writer.Advance(totalSize);
        return totalSize;
    }

    /// <summary>
    /// Encode an Unsubscribe frame:
    /// [1:type=0x07] [4:topicHash] [4:topicLen] [topic UTF-8] [4:subscriberIdLen]
    /// [subscriberId UTF-8] [1:permanent] [4:actorTokenLen] [actorToken UTF-8]
    /// </summary>
    public static int WriteUnsubscribe(
        IBufferWriter<byte> writer,
        string topic,
        string subscriberId,
        bool permanent = true,
        string? actorAccessToken = null)
    {
        actorAccessToken ??= string.Empty;
        var topicBytes = Encoding.UTF8.GetByteCount(topic);
        var idBytes = Encoding.UTF8.GetByteCount(subscriberId);
        var tokenBytes = Encoding.UTF8.GetByteCount(actorAccessToken);
        ValidatePubSubStrings(topicBytes, idBytes, tokenBytes);
        var totalSize = ValidateFrameSize(18L + topicBytes + idBytes + tokenBytes);
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.Unsubscribe;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(1), Fnv1aHash(topic));
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(5), topicBytes);
        Encoding.UTF8.GetBytes(topic, span.Slice(9));
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(9 + topicBytes), idBytes);
        Encoding.UTF8.GetBytes(subscriberId, span.Slice(13 + topicBytes));
        span[13 + topicBytes + idBytes] = permanent ? (byte)0x01 : (byte)0x00;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(14 + topicBytes + idBytes), tokenBytes);
        Encoding.UTF8.GetBytes(actorAccessToken, span.Slice(18 + topicBytes + idBytes));

        writer.Advance(totalSize);
        return totalSize;
    }

    /// <summary>
    /// Encode a Publish frame: [1:type=0x08] [4:topicHash] [1:flags] [4:topicLen] [topic UTF-8] [4:payloadLen] [payload]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WritePublish(IBufferWriter<byte> writer, string topic, bool durableEligible, ReadOnlySpan<byte> payload)
    {
        var topicBytes = Encoding.UTF8.GetByteCount(topic);
        ValidateStringLength(topicBytes, 4096, nameof(topic), allowEmpty: false);
        var totalSize = ValidateFrameSize((long)PublishHeaderSize + topicBytes + payload.Length);
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.Publish;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(1), Fnv1aHash(topic));
        span[5] = (byte)(durableEligible ? 0x01 : 0x00);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(6), topicBytes);
        Encoding.UTF8.GetBytes(topic, span.Slice(10));
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(10 + topicBytes), payload.Length);
        payload.CopyTo(span.Slice(14 + topicBytes));

        writer.Advance(totalSize);
        return totalSize;
    }

    /// <summary>
    /// Encode an Event frame: [1:type=0x09] [4:topicHash] [8:sequenceNumber] [1:flags] [4:subscriberIdLen] [subscriberId UTF-8] [4:payloadLen] [payload]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteEvent(IBufferWriter<byte> writer, int topicHash, long sequenceNumber, bool isReplay, ReadOnlySpan<byte> payload)
        => WriteEvent(writer, topicHash, subscriberId: string.Empty, sequenceNumber, isReplay, payload);

    /// <summary>
    /// Encode an Event frame with a durable subscriber identity. Transient events use an empty subscriber id.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteEvent(
        IBufferWriter<byte> writer,
        int topicHash,
        string? subscriberId,
        long sequenceNumber,
        bool isReplay,
        ReadOnlySpan<byte> payload)
    {
        subscriberId ??= string.Empty;
        var idBytes = Encoding.UTF8.GetByteCount(subscriberId);
        ValidateStringLength(idBytes, 4096, nameof(subscriberId), allowEmpty: true);
        var totalSize = ValidateFrameSize((long)EventHeaderSize + idBytes + payload.Length);
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.Event;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(1), topicHash);
        BinaryPrimitives.WriteInt64LittleEndian(span.Slice(5), sequenceNumber);
        span[13] = (byte)(isReplay ? 0x01 : 0x00);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(14), idBytes);
        Encoding.UTF8.GetBytes(subscriberId, span.Slice(18));
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(18 + idBytes), payload.Length);
        payload.CopyTo(span.Slice(22 + idBytes));

        writer.Advance(totalSize);
        return totalSize;
    }

    /// <summary>
    /// Encode an Ack frame:
    /// [1:type=0x0A] [4:topicHash] [4:topicLen] [topic UTF-8] [4:subscriberIdLen]
    /// [subscriberId UTF-8] [8:upToSequenceNumber] [4:actorTokenLen] [actorToken UTF-8]
    /// </summary>
    public static int WriteAck(
        IBufferWriter<byte> writer,
        string topic,
        string subscriberId,
        long upToSequenceNumber,
        string? actorAccessToken = null)
    {
        actorAccessToken ??= string.Empty;
        var topicBytes = Encoding.UTF8.GetByteCount(topic);
        var idBytes = Encoding.UTF8.GetByteCount(subscriberId);
        var tokenBytes = Encoding.UTF8.GetByteCount(actorAccessToken);
        ValidatePubSubStrings(topicBytes, idBytes, tokenBytes);
        var totalSize = ValidateFrameSize(25L + topicBytes + idBytes + tokenBytes);
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.Ack;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(1), Fnv1aHash(topic));
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(5), topicBytes);
        Encoding.UTF8.GetBytes(topic, span.Slice(9));
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(9 + topicBytes), idBytes);
        Encoding.UTF8.GetBytes(subscriberId, span.Slice(13 + topicBytes));
        BinaryPrimitives.WriteInt64LittleEndian(span.Slice(13 + topicBytes + idBytes), upToSequenceNumber);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(21 + topicBytes + idBytes), tokenBytes);
        Encoding.UTF8.GetBytes(actorAccessToken, span.Slice(25 + topicBytes + idBytes));

        writer.Advance(totalSize);
        return totalSize;
    }

    // ── Streaming ──

    public const int StreamOpenHeaderSize = 1 + 16 + 4 + 4;  // 25 bytes
    public const int StreamDataHeaderSize = 1 + 16 + 4;       // 21 bytes
    public const int StreamCloseSize = 1 + 16 + 2;            // 19 bytes

    /// <summary>
    /// Open a bidirectional stream to a recipient.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteStreamOpen(IBufferWriter<byte> writer, Guid streamId, int recipientHash, int commandHash)
    {
        var span = writer.GetSpan(StreamOpenHeaderSize);
        span[0] = (byte)FrameType.StreamOpen;
        WriteGuid(span.Slice(1), streamId);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(17), recipientHash);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(21), commandHash);
        writer.Advance(StreamOpenHeaderSize);
        return StreamOpenHeaderSize;
    }

    /// <summary>
    /// Write a stream data chunk.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteStreamData(IBufferWriter<byte> writer, Guid streamId, ReadOnlySpan<byte> payload)
    {
        var totalSize = ValidateFrameSize((long)StreamDataHeaderSize + payload.Length);
        var span = writer.GetSpan(totalSize);
        span[0] = (byte)FrameType.StreamData;
        WriteGuid(span.Slice(1), streamId);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(17), payload.Length);
        payload.CopyTo(span.Slice(21));
        writer.Advance(totalSize);
        return totalSize;
    }

    /// <summary>
    /// Close a stream.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteStreamClose(IBufferWriter<byte> writer, Guid streamId, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var span = writer.GetSpan(StreamCloseSize);
        span[0] = (byte)FrameType.StreamClose;
        WriteGuid(span.Slice(1), streamId);
        BinaryPrimitives.WriteInt16LittleEndian(span.Slice(17), (short)statusCode);
        writer.Advance(StreamCloseSize);
        return StreamCloseSize;
    }

    // -- Media encoding --

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteMediaFrame(IBufferWriter<byte> writer, Guid streamId, uint sequenceNumber, uint timestamp, byte flags, ReadOnlySpan<byte> payload)
    {
        var totalSize = ValidateFrameSize((long)MediaFrameHeaderSize + payload.Length);
        var span = writer.GetSpan(totalSize);
        span[0] = (byte)FrameType.MediaFrame;
        WriteGuid(span.Slice(1), streamId);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(17), sequenceNumber);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(21), timestamp);
        span[25] = flags;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(26), payload.Length);
        payload.CopyTo(span.Slice(30));
        writer.Advance(totalSize);
        return totalSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteMediaConfig(IBufferWriter<byte> writer, Guid streamId, Guid callId, MediaType mediaType, CodecId codecId, int param1, int param2, int bitrateKbps, byte flags, ReadOnlySpan<byte> extension)
    {
        var totalSize = ValidateFrameSize((long)MediaConfigHeaderSize + extension.Length);
        var span = writer.GetSpan(totalSize);
        span[0] = (byte)FrameType.MediaConfig;
        WriteGuid(span.Slice(1), streamId);
        WriteGuid(span.Slice(17), callId);
        span[33] = (byte)mediaType;
        span[34] = (byte)codecId;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(35), param1);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(39), param2);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(43), bitrateKbps);
        span[47] = flags;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(48), extension.Length);
        extension.CopyTo(span.Slice(52));
        writer.Advance(totalSize);
        return totalSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteMediaFeedback(IBufferWriter<byte> writer, Guid streamId, uint highestSeqReceived, uint cumulativeLost, uint jitterX100, ushort rttMs, QualityHint qualityHint)
    {
        var span = writer.GetSpan(MediaFeedbackSize);
        span[0] = (byte)FrameType.MediaFeedback;
        WriteGuid(span.Slice(1), streamId);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(17), highestSeqReceived);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(21), cumulativeLost);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(25), jitterX100);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(29), rttMs);
        span[31] = (byte)qualityHint;
        writer.Advance(MediaFeedbackSize);
        return MediaFeedbackSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteMediaKeyRequest(IBufferWriter<byte> writer, Guid streamId)
    {
        var span = writer.GetSpan(MediaKeyRequestSize);
        span[0] = (byte)FrameType.MediaKeyRequest;
        WriteGuid(span.Slice(1), streamId);
        writer.Advance(MediaKeyRequestSize);
        return MediaKeyRequestSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteCallSignal(IBufferWriter<byte> writer, Guid callId, SignalType signalType, ReadOnlySpan<byte> payload)
    {
        var totalSize = ValidateFrameSize((long)CallSignalHeaderSize + payload.Length);
        var span = writer.GetSpan(totalSize);
        span[0] = (byte)FrameType.CallSignal;
        WriteGuid(span.Slice(1), callId);
        span[17] = (byte)signalType;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(18), payload.Length);
        payload.CopyTo(span.Slice(22));
        writer.Advance(totalSize);
        return totalSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteFecFrame(IBufferWriter<byte> writer, Guid streamId, uint fecGroupStart, byte fecGroupSize, ReadOnlySpan<byte> payload)
    {
        var totalSize = ValidateFrameSize((long)FecFrameHeaderSize + payload.Length);
        var span = writer.GetSpan(totalSize);
        span[0] = (byte)FrameType.FecFrame;
        WriteGuid(span.Slice(1), streamId);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(17), fecGroupStart);
        span[21] = fecGroupSize;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(22), payload.Length);
        payload.CopyTo(span.Slice(26));
        writer.Advance(totalSize);
        return totalSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteNackRequest(IBufferWriter<byte> writer, Guid streamId, ReadOnlySpan<uint> missingSequences)
    {
        if (missingSequences.Length > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(missingSequences), "A NACK frame cannot contain more than 65535 sequence numbers.");
        var totalSize = ValidateFrameSize((long)NackRequestHeaderSize + missingSequences.Length * 4L);
        var span = writer.GetSpan(totalSize);
        span[0] = (byte)FrameType.NackRequest;
        WriteGuid(span.Slice(1), streamId);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(17), (ushort)missingSequences.Length);
        for (int i = 0; i < missingSequences.Length; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(19 + i * 4), missingSequences[i]);
        writer.Advance(totalSize);
        return totalSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteRequestCancel(IBufferWriter<byte> writer, Guid requestId)
    {
        var span = writer.GetSpan(RequestCancelSize);
        span[0] = (byte)FrameType.RequestCancel;
        WriteGuid(span.Slice(1), requestId);
        writer.Advance(RequestCancelSize);
        return RequestCancelSize;
    }

    #endregion

    #region Decoding

    /// <summary>
    /// Peek at the frame type without consuming the buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FrameType PeekFrameType(ReadOnlySpan<byte> buffer) => (FrameType)buffer[0];

    private static bool TryAddLength(int headerSize, int payloadLength, out int totalSize)
    {
        totalSize = 0;
        if (payloadLength < 0 || payloadLength > DefaultMaxFrameBytes)
            return false;

        try
        {
            totalSize = checked(headerSize + payloadLength);
        }
        catch (OverflowException)
        {
            return false;
        }

        return totalSize <= DefaultMaxFrameBytes;
    }

    private static int ValidateFrameSize(long totalSize)
    {
        if (totalSize is < 0 or > DefaultMaxFrameBytes)
            throw new ArgumentOutOfRangeException(nameof(totalSize), $"Bolt frames cannot exceed {DefaultMaxFrameBytes} bytes.");
        return (int)totalSize;
    }

    private static void ValidateStringLength(int byteLength, int maximum, string parameterName, bool allowEmpty)
    {
        if ((!allowEmpty && byteLength == 0) || byteLength < 0 || byteLength > maximum)
            throw new ArgumentOutOfRangeException(parameterName, $"UTF-8 length must be between {(allowEmpty ? 0 : 1)} and {maximum} bytes.");
    }

    private static void ValidatePubSubStrings(int topicBytes, int subscriberIdBytes, int tokenBytes)
    {
        ValidateStringLength(topicBytes, 4096, "topic", allowEmpty: false);
        ValidateStringLength(subscriberIdBytes, 4096, "subscriberId", allowEmpty: true);
        ValidateStringLength(tokenBytes, 8192, "actorAccessToken", allowEmpty: true);
    }

    /// <summary>
    /// Try to read a complete request frame. Zero-copy: Payload references the source buffer.
    /// Caller must consume the payload before the buffer is reused.
    /// </summary>
    public static bool TryReadRequest(ReadOnlySpan<byte> buffer, out RequestFrame frame, out int bytesConsumed)
    {
        frame = default;
        bytesConsumed = 0;

        if (buffer.Length < RequestHeaderSize) return false;

        var payloadLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(29));
        if (!TryAddLength(RequestHeaderSize, payloadLen, out var totalSize)) return false;
        if (buffer.Length < totalSize) return false;

        frame = new RequestFrame
        {
            RequestId = ReadGuid(buffer.Slice(1)),
            RecipientHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(17)),
            SenderHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(21)),
            CommandHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(25)),
            PayloadOffset = RequestHeaderSize,
            PayloadLength = payloadLen
        };
        bytesConsumed = totalSize;
        return true;
    }

    /// <summary>
    /// Read only the request header (33 bytes) for routing without touching payload.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadRequestHeader(ReadOnlySpan<byte> buffer, out Guid requestId, out int recipientHash, out int totalSize)
    {
        requestId = default;
        recipientHash = 0;
        totalSize = 0;

        if (buffer.Length < RequestHeaderSize) return false;

        var payloadLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(29));
        if (!TryAddLength(RequestHeaderSize, payloadLen, out totalSize)) return false;
        if (buffer.Length < totalSize) return false;

        requestId = ReadGuid(buffer.Slice(1));
        recipientHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(17));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadRequestCancel(ReadOnlySpan<byte> buffer, out Guid requestId)
    {
        requestId = default;
        if (buffer.Length != RequestCancelSize || buffer[0] != (byte)FrameType.RequestCancel)
            return false;
        requestId = ReadGuid(buffer.Slice(1));
        return true;
    }

    /// <summary>
    /// Read only the response header to extract RequestId for routing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadResponseHeader(ReadOnlySpan<byte> buffer, out Guid requestId, out int totalSize)
    {
        requestId = default;
        totalSize = 0;

        if (buffer.Length < ResponseHeaderSize) return false;

        var payloadLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(19));
        if (!TryAddLength(ResponseHeaderSize, payloadLen, out totalSize)) return false;
        if (buffer.Length < totalSize) return false;

        requestId = ReadGuid(buffer.Slice(1));
        return true;
    }

    /// <summary>
    /// Try to read a complete response frame. Zero-copy: Payload references the source buffer.
    /// </summary>
    public static bool TryReadResponse(ReadOnlySpan<byte> buffer, out ResponseFrame frame, out int bytesConsumed)
    {
        frame = default;
        bytesConsumed = 0;

        if (buffer.Length < ResponseHeaderSize) return false;

        var payloadLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(19));
        if (!TryAddLength(ResponseHeaderSize, payloadLen, out var totalSize)) return false;
        if (buffer.Length < totalSize) return false;

        frame = new ResponseFrame
        {
            RequestId = ReadGuid(buffer.Slice(1)),
            StatusCode = (HttpStatusCode)BinaryPrimitives.ReadInt16LittleEndian(buffer.Slice(17)),
            PayloadOffset = ResponseHeaderSize,
            PayloadLength = payloadLen
        };
        bytesConsumed = totalSize;
        return true;
    }

    /// <summary>
    /// Try to read a register frame.
    /// </summary>
    public static bool TryReadRegister(
        ReadOnlySpan<byte> buffer,
        out ushort wireVersion,
        out string clientId,
        out string clientName,
        out int bytesConsumed)
    {
        wireVersion = 0;
        clientId = "";
        clientName = "";
        bytesConsumed = 0;

        if (buffer.Length < 11 || buffer[0] != (byte)FrameType.Register) return false;

        wireVersion = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(1));
        var idLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(3));
        if (idLen < 0 || idLen > DefaultMaxStringBytes) return false;
        if (!TryAddLength(11, idLen, out var minimumRegisterSize) || buffer.Length < minimumRegisterSize) return false;

        var nameLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(7 + idLen));
        if (nameLen < 0 || nameLen > DefaultMaxStringBytes) return false;
        if (!TryAddLength(11 + idLen, nameLen, out var totalSize)) return false;
        if (buffer.Length < totalSize) return false;

        clientId = Encoding.UTF8.GetString(buffer.Slice(7, idLen));
        clientName = Encoding.UTF8.GetString(buffer.Slice(11 + idLen, nameLen));
        bytesConsumed = totalSize;
        return true;
    }

    public static bool TryReadRegister(
        ReadOnlySpan<byte> buffer,
        out string clientId,
        out string clientName,
        out int bytesConsumed) =>
        TryReadRegister(buffer, out _, out clientId, out clientName, out bytesConsumed);

    public static bool TryReadRegisterAck(
        ReadOnlySpan<byte> buffer,
        out bool success,
        out ushort wireVersion)
    {
        success = false;
        wireVersion = 0;
        if (buffer.Length != RegisterAckSize || buffer[0] != (byte)FrameType.RegisterAck)
            return false;

        success = buffer[1] == 1;
        wireVersion = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(2));
        return buffer[1] is 0 or 1;
    }

    public static bool TryReadBatch(ReadOnlySpan<byte> buffer, out BoltBatchFrame batch)
    {
        batch = default;
        if (buffer.Length is < BatchHeaderSize or > MaxBatchBytes || buffer[0] != (byte)FrameType.Batch)
            return false;

        var count = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(1));
        if (count is < 2 or > MaxBatchFrames)
            return false;

        var offset = BatchHeaderSize;
        for (var i = 0; i < count; i++)
        {
            if (offset > buffer.Length - 4)
                return false;

            var length = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(offset));
            offset += 4;
            if (length <= 0 || offset > buffer.Length - length)
                return false;
            var innerFrame = buffer.Slice(offset, length);
            if (!IsValidBatchInnerFrame(innerFrame))
                return false;

            offset += length;
        }

        if (offset != buffer.Length)
            return false;

        batch = new BoltBatchFrame(buffer.Slice(BatchHeaderSize), count);
        return true;
    }

    public static bool IsBatchableFrameType(FrameType frameType) => frameType is
        FrameType.Request or
        FrameType.Response or
        FrameType.Push or
        FrameType.Subscribe or
        FrameType.Unsubscribe or
        FrameType.Publish or
        FrameType.Event or
        FrameType.Ack or
        FrameType.RequestCancel or
        FrameType.StreamOpen or
        FrameType.StreamData or
        FrameType.StreamClose;

    public static bool IsValidBatchInnerFrame(ReadOnlySpan<byte> frame) =>
        !frame.IsEmpty &&
        IsBatchableFrameType((FrameType)frame[0]) &&
        IsCompleteBatchInnerFrame(frame);

    private static bool IsCompleteBatchInnerFrame(ReadOnlySpan<byte> frame)
    {
        switch ((FrameType)frame[0])
        {
            case FrameType.Request:
            case FrameType.Push:
                return TryReadRequest(frame, out _, out var requestSize) && requestSize == frame.Length;
            case FrameType.Response:
                return TryReadResponse(frame, out _, out var responseSize) && responseSize == frame.Length;
            case FrameType.Subscribe:
                return TryReadSubscribe(frame, out _, out _, out _, out _, out _, out var subscribeSize) &&
                       subscribeSize == frame.Length;
            case FrameType.Unsubscribe:
                return TryReadUnsubscribe(frame, out _, out _, out _, out _, out _, out var unsubscribeSize) &&
                       unsubscribeSize == frame.Length;
            case FrameType.Publish:
                return TryReadPublish(frame, out _, out _, out _, out _, out _, out var publishSize) &&
                       publishSize == frame.Length;
            case FrameType.Event:
                return TryReadEvent(frame, out _, out _, out _, out _, out _, out _, out var eventSize) &&
                       eventSize == frame.Length;
            case FrameType.Ack:
                return TryReadAck(frame, out _, out _, out _, out _, out var ackSize) && ackSize == frame.Length;
            case FrameType.RequestCancel:
                return TryReadRequestCancel(frame, out _);
            case FrameType.StreamOpen:
                return frame.Length == StreamOpenHeaderSize && TryReadStreamOpen(frame, out _, out _, out _);
            case FrameType.StreamData:
                return TryReadStreamData(frame, out _, out _, out _, out var streamDataSize) &&
                       streamDataSize == frame.Length;
            case FrameType.StreamClose:
                return frame.Length == StreamCloseSize && TryReadStreamClose(frame, out _, out _);
            default:
                return false;
        }
    }

    // ── Stream frame decoding ──

    /// <summary>
    /// Read a StreamOpen frame.
    /// </summary>
    public static bool TryReadStreamOpen(ReadOnlySpan<byte> buffer, out Guid streamId, out int recipientHash, out int commandHash)
    {
        streamId = default;
        recipientHash = 0;
        commandHash = 0;
        if (buffer.Length < StreamOpenHeaderSize) return false;

        streamId = ReadGuid(buffer.Slice(1));
        recipientHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(17));
        commandHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(21));
        return true;
    }

    /// <summary>
    /// Read a StreamData frame header. Payload is at offset 21.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadStreamData(ReadOnlySpan<byte> buffer, out Guid streamId, out int payloadOffset, out int payloadLength, out int totalSize)
    {
        streamId = default;
        payloadOffset = 0;
        payloadLength = 0;
        totalSize = 0;

        if (buffer.Length < StreamDataHeaderSize) return false;

        streamId = ReadGuid(buffer.Slice(1));
        payloadLength = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(17));
        if (!TryAddLength(StreamDataHeaderSize, payloadLength, out totalSize)) return false;
        payloadOffset = StreamDataHeaderSize;
        return buffer.Length >= totalSize;
    }

    /// <summary>
    /// Read a StreamClose frame.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadStreamClose(ReadOnlySpan<byte> buffer, out Guid streamId, out HttpStatusCode statusCode)
    {
        streamId = default;
        statusCode = default;
        if (buffer.Length < StreamCloseSize) return false;

        streamId = ReadGuid(buffer.Slice(1));
        statusCode = (HttpStatusCode)BinaryPrimitives.ReadInt16LittleEndian(buffer.Slice(17));
        return true;
    }

    /// <summary>
    /// Read just the streamId from any stream frame (bytes 1-16).
    /// Used by hub for routing without full decode.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guid ReadStreamId(ReadOnlySpan<byte> buffer) => ReadGuid(buffer.Slice(1));

    // -- Media decoding --

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadMediaFrame(ReadOnlySpan<byte> buffer, out MediaFrameHeader header)
    {
        header = default;
        if (buffer.Length < MediaFrameHeaderSize) return false;

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(26));
        if (!TryAddLength(MediaFrameHeaderSize, payloadLength, out var totalSize) || buffer.Length < totalSize) return false;

        header = new MediaFrameHeader
        {
            StreamId = ReadGuid(buffer.Slice(1)),
            SequenceNumber = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(17)),
            Timestamp = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(21)),
            Flags = buffer[25],
            PayloadOffset = MediaFrameHeaderSize,
            PayloadLength = payloadLength,
        };
        return true;
    }

    /// <summary>Header-only read for hub routing — only extracts streamId.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadMediaFrameHeader(ReadOnlySpan<byte> buffer, out Guid streamId)
    {
        streamId = default;
        if (buffer.Length < 17) return false;
        streamId = ReadGuid(buffer.Slice(1));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadMediaConfig(ReadOnlySpan<byte> buffer, out MediaConfigData config)
    {
        config = default;
        if (buffer.Length < MediaConfigHeaderSize) return false;

        var extensionLength = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(48));
        if (!TryAddLength(MediaConfigHeaderSize, extensionLength, out var totalSize) || buffer.Length < totalSize) return false;

        config = new MediaConfigData
        {
            StreamId = ReadGuid(buffer.Slice(1)),
            CallId = ReadGuid(buffer.Slice(17)),
            MediaType = (MediaType)buffer[33],
            CodecId = (CodecId)buffer[34],
            Param1 = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(35)),
            Param2 = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(39)),
            BitrateKbps = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(43)),
            Flags = buffer[47],
            ExtensionOffset = MediaConfigHeaderSize,
            ExtensionLength = extensionLength,
        };
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadMediaFeedback(ReadOnlySpan<byte> buffer, out MediaFeedbackData feedback)
    {
        feedback = default;
        if (buffer.Length < MediaFeedbackSize) return false;

        feedback = new MediaFeedbackData
        {
            StreamId = ReadGuid(buffer.Slice(1)),
            HighestSeqReceived = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(17)),
            CumulativeLost = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(21)),
            JitterX100 = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(25)),
            RttMs = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(29)),
            QualityHint = (QualityHint)buffer[31],
        };
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadMediaKeyRequest(ReadOnlySpan<byte> buffer, out Guid streamId)
    {
        streamId = default;
        if (buffer.Length < MediaKeyRequestSize) return false;
        streamId = ReadGuid(buffer.Slice(1));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadCallSignal(ReadOnlySpan<byte> buffer, out CallSignalHeader header)
    {
        header = default;
        if (buffer.Length < CallSignalHeaderSize) return false;

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(18));
        if (!TryAddLength(CallSignalHeaderSize, payloadLength, out var totalSize) || buffer.Length < totalSize) return false;

        header = new CallSignalHeader
        {
            CallId = ReadGuid(buffer.Slice(1)),
            SignalType = (SignalType)buffer[17],
            PayloadOffset = CallSignalHeaderSize,
            PayloadLength = payloadLength,
        };
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadFecFrame(ReadOnlySpan<byte> buffer, out FecFrameHeader header)
    {
        header = default;
        if (buffer.Length < FecFrameHeaderSize) return false;

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(22));
        if (!TryAddLength(FecFrameHeaderSize, payloadLength, out var totalSize) || buffer.Length < totalSize) return false;

        header = new FecFrameHeader
        {
            StreamId = ReadGuid(buffer.Slice(1)),
            FecGroupStart = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(17)),
            FecGroupSize = buffer[21],
            PayloadOffset = FecFrameHeaderSize,
            PayloadLength = payloadLength,
        };
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadNackRequest(ReadOnlySpan<byte> buffer, out NackRequestHeader header)
    {
        header = default;
        if (buffer.Length < NackRequestHeaderSize) return false;

        var nackCount = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(17));
        if (!TryAddLength(NackRequestHeaderSize, nackCount * 4, out var totalSize)) return false;
        if (buffer.Length < totalSize) return false;

        header = new NackRequestHeader
        {
            StreamId = ReadGuid(buffer.Slice(1)),
            NackCount = nackCount,
            SequencesOffset = NackRequestHeaderSize,
        };
        return true;
    }

    // ── Pub/sub decoding ──

    /// <summary>
    /// Decode a Subscribe frame.
    /// </summary>
    public static bool TryReadSubscribe(
        ReadOnlySpan<byte> buffer,
        out int topicHash,
        out bool durable,
        out string subscriberId,
        out string topic,
        out int bytesConsumed) =>
        TryReadSubscribe(
            buffer,
            out topicHash,
            out durable,
            out subscriberId,
            out topic,
            out _,
            out bytesConsumed);

    public static bool TryReadSubscribe(
        ReadOnlySpan<byte> buffer,
        out int topicHash,
        out bool durable,
        out string subscriberId,
        out string topic,
        out string actorAccessToken,
        out int bytesConsumed)
    {
        topicHash = 0;
        durable = false;
        subscriberId = string.Empty;
        topic = string.Empty;
        actorAccessToken = string.Empty;
        bytesConsumed = 0;

        if (buffer.Length < 14) return false;
        if (buffer[0] != (byte)FrameType.Subscribe) return false;

        topicHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(1));
        durable = (buffer[5] & 0x01) != 0;
        var idLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(6));
        if (idLen < 0 || idLen > 4096) return false;
        if (buffer.Length < 10 + idLen + 4) return false;

        subscriberId = Encoding.UTF8.GetString(buffer.Slice(10, idLen));
        var topicLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(10 + idLen));
        if (topicLen < 0 || topicLen > 4096) return false;
        if (buffer.Length < 14 + idLen + topicLen) return false;

        topic = Encoding.UTF8.GetString(buffer.Slice(14 + idLen, topicLen));
        if (Fnv1aHash(topic) != topicHash) return false;

        bytesConsumed = 14 + idLen + topicLen;

        if (buffer.Length >= bytesConsumed + 4)
        {
            var tokenLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(bytesConsumed));
            if (tokenLen < 0 || tokenLen > 8192) return false;
            if (buffer.Length < bytesConsumed + 4 + tokenLen) return false;

            actorAccessToken = Encoding.UTF8.GetString(buffer.Slice(bytesConsumed + 4, tokenLen));
            bytesConsumed += 4 + tokenLen;
        }

        return true;
    }

    /// <summary>
    /// Decode an Unsubscribe frame.
    /// </summary>
    public static bool TryReadUnsubscribe(ReadOnlySpan<byte> buffer, out int topicHash, out string topic, out string subscriberId, out int bytesConsumed) =>
        TryReadUnsubscribe(buffer, out topicHash, out topic, out subscriberId, out _, out bytesConsumed);

    /// <summary>
    /// Decode an Unsubscribe frame.
    /// </summary>
    public static bool TryReadUnsubscribe(
        ReadOnlySpan<byte> buffer,
        out int topicHash,
        out string topic,
        out string subscriberId,
        out bool permanent,
        out int bytesConsumed) =>
        TryReadUnsubscribe(
            buffer,
            out topicHash,
            out topic,
            out subscriberId,
            out permanent,
            out _,
            out bytesConsumed);

    public static bool TryReadUnsubscribe(
        ReadOnlySpan<byte> buffer,
        out int topicHash,
        out string topic,
        out string subscriberId,
        out bool permanent,
        out string actorAccessToken,
        out int bytesConsumed)
    {
        topicHash = 0;
        topic = string.Empty;
        subscriberId = string.Empty;
        permanent = true;
        actorAccessToken = string.Empty;
        bytesConsumed = 0;

        if (buffer.Length < 13) return false;
        if (buffer[0] != (byte)FrameType.Unsubscribe) return false;

        topicHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(1));
        var topicLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(5));
        if (topicLen <= 0 || topicLen > 4096) return false;
        if (buffer.Length < 9 + topicLen + 4) return false;

        topic = Encoding.UTF8.GetString(buffer.Slice(9, topicLen));
        if (Fnv1aHash(topic) != topicHash) return false;

        var idOffset = 9 + topicLen;
        var idLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(idOffset));
        if (idLen < 0 || idLen > 4096) return false;
        if (buffer.Length < idOffset + 4 + idLen) return false;

        subscriberId = Encoding.UTF8.GetString(buffer.Slice(idOffset + 4, idLen));
        var permanentOffset = idOffset + 4 + idLen;
        if (buffer.Length > permanentOffset)
        {
            permanent = buffer[permanentOffset] != 0;
            bytesConsumed = permanentOffset + 1;
        }
        else
        {
            // Backward-compatible old unsubscribe frames permanently unregister.
            permanent = true;
            bytesConsumed = permanentOffset;
        }

        if (buffer.Length >= bytesConsumed + 4)
        {
            var tokenLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(bytesConsumed));
            if (tokenLen < 0 || tokenLen > 8192) return false;
            if (buffer.Length < bytesConsumed + 4 + tokenLen) return false;

            actorAccessToken = Encoding.UTF8.GetString(buffer.Slice(bytesConsumed + 4, tokenLen));
            bytesConsumed += 4 + tokenLen;
        }
        return true;
    }

    /// <summary>
    /// Decode a Publish frame. Returns offset/length into the source buffer (zero-copy).
    /// </summary>
    public static bool TryReadPublish(ReadOnlySpan<byte> buffer, out int topicHash, out string topic, out bool durableEligible, out int payloadOffset, out int payloadLength, out int totalSize)
    {
        topicHash = 0;
        topic = string.Empty;
        durableEligible = false;
        payloadOffset = 0;
        payloadLength = 0;
        totalSize = 0;

        if (buffer.Length < PublishHeaderSize) return false;
        if (buffer[0] != (byte)FrameType.Publish) return false;

        topicHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(1));
        durableEligible = (buffer[5] & 0x01) != 0;
        var topicLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(6));
        if (topicLen <= 0 || topicLen > 4096) return false;
        if (buffer.Length < 10 + topicLen + 4) return false;

        topic = Encoding.UTF8.GetString(buffer.Slice(10, topicLen));
        if (Fnv1aHash(topic) != topicHash) return false;

        payloadLength = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(10 + topicLen));
        if (payloadLength < 0 || payloadLength > DefaultMaxFrameBytes) return false;

        payloadOffset = 14 + topicLen;
        totalSize = payloadOffset + payloadLength;
        return buffer.Length >= totalSize;
    }

    /// <summary>
    /// Decode an Event frame. Returns offset/length into the source buffer (zero-copy).
    /// </summary>
    public static bool TryReadEvent(ReadOnlySpan<byte> buffer, out int topicHash, out long sequenceNumber, out bool isReplay, out int payloadOffset, out int payloadLength, out int totalSize)
        => TryReadEvent(buffer, out topicHash, out sequenceNumber, out isReplay, out _, out payloadOffset, out payloadLength, out totalSize);

    /// <summary>
    /// Decode an Event frame including the durable subscriber identity when present.
    /// </summary>
    public static bool TryReadEvent(
        ReadOnlySpan<byte> buffer,
        out int topicHash,
        out long sequenceNumber,
        out bool isReplay,
        out string subscriberId,
        out int payloadOffset,
        out int payloadLength,
        out int totalSize)
    {
        topicHash = 0;
        sequenceNumber = 0;
        isReplay = false;
        subscriberId = string.Empty;
        payloadOffset = 0;
        payloadLength = 0;
        totalSize = 0;

        if (buffer.Length < EventHeaderSize) return false;
        if (buffer[0] != (byte)FrameType.Event) return false;

        topicHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(1));
        sequenceNumber = BinaryPrimitives.ReadInt64LittleEndian(buffer.Slice(5));
        isReplay = (buffer[13] & 0x01) != 0;
        var idLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(14));
        if (idLen < 0 || idLen > 4096) return false;
        if (buffer.Length < 18 + idLen + 4) return false;

        subscriberId = Encoding.UTF8.GetString(buffer.Slice(18, idLen));
        payloadLength = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(18 + idLen));
        if (payloadLength < 0 || payloadLength > DefaultMaxFrameBytes) return false;

        payloadOffset = EventHeaderSize + idLen;
        totalSize = payloadOffset + payloadLength;
        return buffer.Length >= totalSize;
    }

    /// <summary>
    /// Decode an Ack frame.
    /// </summary>
    public static bool TryReadAck(
        ReadOnlySpan<byte> buffer,
        out int topicHash,
        out string topic,
        out string subscriberId,
        out long upToSequenceNumber,
        out int bytesConsumed) =>
        TryReadAck(
            buffer,
            out topicHash,
            out topic,
            out subscriberId,
            out upToSequenceNumber,
            out _,
            out bytesConsumed);

    public static bool TryReadAck(
        ReadOnlySpan<byte> buffer,
        out int topicHash,
        out string topic,
        out string subscriberId,
        out long upToSequenceNumber,
        out string actorAccessToken,
        out int bytesConsumed)
    {
        topicHash = 0;
        topic = string.Empty;
        subscriberId = string.Empty;
        upToSequenceNumber = 0;
        actorAccessToken = string.Empty;
        bytesConsumed = 0;

        if (buffer.Length < 13) return false;
        if (buffer[0] != (byte)FrameType.Ack) return false;

        topicHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(1));
        var topicLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(5));
        if (topicLen <= 0 || topicLen > 4096) return false;
        if (buffer.Length < 9 + topicLen + 4) return false;

        topic = Encoding.UTF8.GetString(buffer.Slice(9, topicLen));
        if (Fnv1aHash(topic) != topicHash) return false;

        var idOffset = 9 + topicLen;
        var idLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(idOffset));
        if (idLen < 0 || idLen > 4096) return false;
        if (buffer.Length < idOffset + 4 + idLen + 8) return false;

        subscriberId = Encoding.UTF8.GetString(buffer.Slice(idOffset + 4, idLen));
        upToSequenceNumber = BinaryPrimitives.ReadInt64LittleEndian(buffer.Slice(idOffset + 4 + idLen));
        bytesConsumed = idOffset + 4 + idLen + 8;

        if (buffer.Length >= bytesConsumed + 4)
        {
            var tokenLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(bytesConsumed));
            if (tokenLen < 0 || tokenLen > 8192) return false;
            if (buffer.Length < bytesConsumed + 4 + tokenLen) return false;

            actorAccessToken = Encoding.UTF8.GetString(buffer.Slice(bytesConsumed + 4, tokenLen));
            bytesConsumed += 4 + tokenLen;
        }

        return true;
    }

    #endregion

    #region Hashing

    /// <summary>
    /// FNV-1a hash for routing. Consistent, fast, no allocations.
    /// Used for both command names and recipient service IDs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Fnv1aHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in value)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)hash;
        }
    }

    #endregion

    #region Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteGuid(Span<byte> dest, Guid guid)
    {
        guid.TryWriteBytes(dest);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Guid ReadGuid(ReadOnlySpan<byte> src)
    {
        return new Guid(src.Slice(0, 16));
    }

    #endregion
}

/// <summary>
/// Decoded request frame. Zero-copy: payload is an offset+length into the source buffer.
/// Use GetPayload(sourceBuffer) to read the payload.
/// </summary>
public struct RequestFrame
{
    public Guid RequestId;
    public int RecipientHash;
    public int SenderHash;
    public int CommandHash;
    public int PayloadOffset;
    public int PayloadLength;

    /// <summary>
    /// Get the payload slice from the original buffer. Zero-copy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetPayload(ReadOnlySpan<byte> sourceBuffer)
        => sourceBuffer.Slice(PayloadOffset, PayloadLength);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<byte> GetPayload(ReadOnlyMemory<byte> sourceBuffer)
        => sourceBuffer.Slice(PayloadOffset, PayloadLength);
}

/// <summary>
/// Decoded response frame. Zero-copy: payload is an offset+length into the source buffer.
/// </summary>
public struct ResponseFrame
{
    public Guid RequestId;
    public HttpStatusCode StatusCode;
    public int PayloadOffset;
    public int PayloadLength;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetPayload(ReadOnlySpan<byte> sourceBuffer)
        => sourceBuffer.Slice(PayloadOffset, PayloadLength);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<byte> GetPayload(ReadOnlyMemory<byte> sourceBuffer)
        => sourceBuffer.Slice(PayloadOffset, PayloadLength);
}

// -- Media frame structs --

/// <summary>Decoded media frame header. Zero-copy payload.</summary>
public struct MediaFrameHeader
{
    public Guid StreamId;
    public uint SequenceNumber;
    public uint Timestamp;
    public byte Flags;
    public int PayloadOffset;
    public int PayloadLength;

    public bool IsKeyframe => (Flags & 0x01) != 0;
    public bool IsFecProtected => (Flags & 0x08) != 0;
    public bool IsEncrypted => (Flags & 0x10) != 0;
    public bool IsDropEligible => (Flags & 0x40) != 0;
    public bool IsCompressed => (Flags & 0x80) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetPayload(ReadOnlySpan<byte> sourceBuffer)
        => sourceBuffer.Slice(PayloadOffset, PayloadLength);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<byte> GetPayload(ReadOnlyMemory<byte> sourceBuffer)
        => sourceBuffer.Slice(PayloadOffset, PayloadLength);
}

/// <summary>Decoded media config data.</summary>
public struct MediaConfigData
{
    public Guid StreamId;
    public Guid CallId;
    public MediaType MediaType;
    public CodecId CodecId;
    public int Param1;          // SampleRate (audio) or Width (video)
    public int Param2;          // ChannelCount (audio) or Height (video)
    public int BitrateKbps;
    public byte Flags;
    public int ExtensionOffset;
    public int ExtensionLength;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetExtension(ReadOnlySpan<byte> sourceBuffer)
        => sourceBuffer.Slice(ExtensionOffset, ExtensionLength);
}

/// <summary>Decoded media feedback (fixed size, no payload).</summary>
public struct MediaFeedbackData
{
    public Guid StreamId;
    public uint HighestSeqReceived;
    public uint CumulativeLost;
    public uint JitterX100;
    public ushort RttMs;
    public QualityHint QualityHint;
}

/// <summary>Decoded call signal header. Zero-copy payload.</summary>
public struct CallSignalHeader
{
    public Guid CallId;
    public SignalType SignalType;
    public int PayloadOffset;
    public int PayloadLength;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetPayload(ReadOnlySpan<byte> sourceBuffer)
        => sourceBuffer.Slice(PayloadOffset, PayloadLength);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<byte> GetPayload(ReadOnlyMemory<byte> sourceBuffer)
        => sourceBuffer.Slice(PayloadOffset, PayloadLength);
}

/// <summary>Decoded FEC frame header. Zero-copy payload.</summary>
public struct FecFrameHeader
{
    public Guid StreamId;
    public uint FecGroupStart;
    public byte FecGroupSize;
    public int PayloadOffset;
    public int PayloadLength;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetPayload(ReadOnlySpan<byte> sourceBuffer)
        => sourceBuffer.Slice(PayloadOffset, PayloadLength);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<byte> GetPayload(ReadOnlyMemory<byte> sourceBuffer)
        => sourceBuffer.Slice(PayloadOffset, PayloadLength);
}

/// <summary>Decoded NACK request header. Missing sequences start at SequencesOffset.</summary>
public struct NackRequestHeader
{
    public Guid StreamId;
    public ushort NackCount;
    public int SequencesOffset;

    /// <summary>Read the missing sequence numbers from the source buffer.</summary>
    public uint[] GetMissingSequences(ReadOnlySpan<byte> sourceBuffer)
    {
        var seqs = new uint[NackCount];
        for (int i = 0; i < NackCount; i++)
            seqs[i] = BinaryPrimitives.ReadUInt32LittleEndian(sourceBuffer.Slice(SequencesOffset + i * 4));
        return seqs;
    }
}
