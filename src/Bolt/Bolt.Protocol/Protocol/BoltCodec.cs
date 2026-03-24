using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace Bolt.Protocol;

/// <summary>
/// Zero-allocation binary codec for the thin StreamFlow protocol.
///
/// Request frame:  [1:type] [16:requestId] [4:recipientHash] [4:commandHash] [4:payloadLen] [payload]
///                 = 29 bytes header + N payload
///
/// Response frame: [1:type] [16:requestId] [2:statusCode] [4:payloadLen] [payload]
///                 = 23 bytes header + N payload
///
/// Register frame: [1:type] [4:clientIdLen] [clientId UTF-8] [4:clientNameLen] [clientName UTF-8]
///
/// All multi-byte values are little-endian.
/// </summary>
public static class BoltCodec
{
    public const int RequestHeaderSize = 1 + 16 + 4 + 4 + 4;   // 29 bytes
    public const int ResponseHeaderSize = 1 + 16 + 2 + 4;       // 23 bytes

    #region Encoding

    /// <summary>
    /// Encode a request frame into the provided buffer writer.
    /// Returns the total bytes written.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteRequest(IBufferWriter<byte> writer, Guid requestId, int recipientHash, int commandHash, ReadOnlySpan<byte> payload)
    {
        var totalSize = RequestHeaderSize + payload.Length;
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.Request;
        WriteGuid(span.Slice(1), requestId);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(17), recipientHash);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(21), commandHash);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(25), payload.Length);
        payload.CopyTo(span.Slice(29));

        writer.Advance(totalSize);
        return totalSize;
    }

    /// <summary>
    /// Encode a response frame into the provided buffer writer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteResponse(IBufferWriter<byte> writer, Guid requestId, HttpStatusCode statusCode, ReadOnlySpan<byte> payload)
    {
        var totalSize = ResponseHeaderSize + payload.Length;
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
        var totalSize = 1 + 4 + idBytes + 4 + nameBytes;
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.Register;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(1), idBytes);
        Encoding.UTF8.GetBytes(clientId, span.Slice(5));
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(5 + idBytes), nameBytes);
        Encoding.UTF8.GetBytes(clientName, span.Slice(9 + idBytes));

        writer.Advance(totalSize);
        return totalSize;
    }

    /// <summary>
    /// Encode a register acknowledgement.
    /// </summary>
    public static int WriteRegisterAck(IBufferWriter<byte> writer, bool success)
    {
        var span = writer.GetSpan(2);
        span[0] = (byte)FrameType.RegisterAck;
        span[1] = success ? (byte)1 : (byte)0;
        writer.Advance(2);
        return 2;
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
        var totalSize = StreamDataHeaderSize + payload.Length;
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

    #endregion

    #region Decoding

    /// <summary>
    /// Peek at the frame type without consuming the buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FrameType PeekFrameType(ReadOnlySpan<byte> buffer) => (FrameType)buffer[0];

    /// <summary>
    /// Try to read a complete request frame. Zero-copy: Payload references the source buffer.
    /// Caller must consume the payload before the buffer is reused.
    /// </summary>
    public static bool TryReadRequest(ReadOnlySpan<byte> buffer, out RequestFrame frame, out int bytesConsumed)
    {
        frame = default;
        bytesConsumed = 0;

        if (buffer.Length < RequestHeaderSize) return false;

        var payloadLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(25));
        var totalSize = RequestHeaderSize + payloadLen;
        if (buffer.Length < totalSize) return false;

        frame = new RequestFrame
        {
            RequestId = ReadGuid(buffer.Slice(1)),
            RecipientHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(17)),
            CommandHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(21)),
            PayloadOffset = RequestHeaderSize,
            PayloadLength = payloadLen
        };
        bytesConsumed = totalSize;
        return true;
    }

    /// <summary>
    /// Read only the request header (29 bytes) for routing without touching payload.
    /// Used by the server to extract RequestId + RecipientHash for forwarding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadRequestHeader(ReadOnlySpan<byte> buffer, out Guid requestId, out int recipientHash, out int totalSize)
    {
        requestId = default;
        recipientHash = 0;
        totalSize = 0;

        if (buffer.Length < RequestHeaderSize) return false;

        var payloadLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(25));
        totalSize = RequestHeaderSize + payloadLen;
        if (buffer.Length < totalSize) return false;

        requestId = ReadGuid(buffer.Slice(1));
        recipientHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(17));
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
        totalSize = ResponseHeaderSize + payloadLen;
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
        var totalSize = ResponseHeaderSize + payloadLen;
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
    public static bool TryReadRegister(ReadOnlySpan<byte> buffer, out string clientId, out string clientName, out int bytesConsumed)
    {
        clientId = "";
        clientName = "";
        bytesConsumed = 0;

        if (buffer.Length < 9) return false; // 1 + 4 + at least 4

        var idLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(1));
        if (buffer.Length < 9 + idLen) return false;

        var nameLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(5 + idLen));
        var totalSize = 9 + idLen + nameLen;
        if (buffer.Length < totalSize) return false;

        clientId = Encoding.UTF8.GetString(buffer.Slice(5, idLen));
        clientName = Encoding.UTF8.GetString(buffer.Slice(9 + idLen, nameLen));
        bytesConsumed = totalSize;
        return true;
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
        payloadOffset = StreamDataHeaderSize;
        totalSize = StreamDataHeaderSize + payloadLength;
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
