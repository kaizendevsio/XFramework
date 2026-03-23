using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace StreamFlow.Domain.Shared.Protocol;

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
public static class StreamFlowCodec
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

    #endregion

    #region Decoding

    /// <summary>
    /// Peek at the frame type without consuming the buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FrameType PeekFrameType(ReadOnlySpan<byte> buffer) => (FrameType)buffer[0];

    /// <summary>
    /// Try to read a complete request frame. Returns false if buffer is incomplete.
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
            Payload = buffer.Slice(29, payloadLen).ToArray()
        };
        bytesConsumed = totalSize;
        return true;
    }

    /// <summary>
    /// Try to read a complete response frame.
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
            Payload = buffer.Slice(23, payloadLen).ToArray()
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
/// Decoded request frame.
/// </summary>
public struct RequestFrame
{
    public Guid RequestId;
    public int RecipientHash;
    public int CommandHash;
    public ReadOnlyMemory<byte> Payload;
}

/// <summary>
/// Decoded response frame.
/// </summary>
public struct ResponseFrame
{
    public Guid RequestId;
    public HttpStatusCode StatusCode;
    public ReadOnlyMemory<byte> Payload;
}
