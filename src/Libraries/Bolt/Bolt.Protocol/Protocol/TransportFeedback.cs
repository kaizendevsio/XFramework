using System.Buffers.Binary;

namespace Bolt.Protocol;

/// <summary>
/// <see cref="FrameType.TransportSequenced"/>: one data-channel message from a participant, stamped with a
/// transport-wide sequence number so the relay can say when it arrived: [1:type] [2:sequence] [message]. The message
/// inside is exactly what would have been sent without the stamp (a MediaFrame, a bundle, feedback). Only between a
/// participant and a relay that announced <see cref="MediaTransportFeatures.TransportFeedback"/>.
/// </summary>
public static class TransportSequenceCodec
{
    public const int HeaderSize = 3;

    public static int Write(Span<byte> destination, ushort sequence, ReadOnlySpan<byte> message)
    {
        if (destination.Length < HeaderSize + message.Length) throw new ArgumentException("The message does not fit.", nameof(destination));
        destination[0] = (byte)FrameType.TransportSequenced;
        BinaryPrimitives.WriteUInt16LittleEndian(destination[1..], sequence);
        message.CopyTo(destination[HeaderSize..]);
        return HeaderSize + message.Length;
    }

    public static bool TryRead(ReadOnlySpan<byte> frame, out ushort sequence, out ReadOnlySpan<byte> message)
    {
        sequence = 0;
        message = default;
        if (frame.Length <= HeaderSize || frame[0] != (byte)FrameType.TransportSequenced) return false;
        sequence = BinaryPrimitives.ReadUInt16LittleEndian(frame[1..]);
        message = frame[HeaderSize..];
        // One stamp per message: a stamped stamp is malformed.
        return message[0] != (byte)FrameType.TransportSequenced;
    }
}

/// <summary>
/// <see cref="FrameType.TransportFeedback"/>: when each transport-sequenced message from a participant reached the relay,
/// the compact way: [1:type] [1:version] [2:base sequence] [2:count] [4:reference, relay clock in ms]
/// [ceil(count / 8):received bitmap, bit i of byte i / 8 for base + i] then, for each received message in order, its
/// arrival after the previous one (the first: after the reference) in 250 µs units, one byte when 0-254, otherwise
/// 0xFF and a signed 16-bit value. About 1.1 bytes a message; a report every 100 ms.
/// </summary>
public static class TransportFeedbackCodec
{
    public const byte Version = 1;
    public const int HeaderSize = 1 + 1 + 2 + 2 + 4;
    public const int MaxPackets = 1024;
    /// <summary>Arrival resolution: 250 µs, as in RTP transport-wide congestion control.</summary>
    public const int TickMicroseconds = 250;

    /// <summary>Encode a report. <paramref name="arrivalMicroseconds"/> holds one entry per sequence from <paramref name="baseSequence"/>: the relay clock in µs, or a negative value for a message that did not arrive.</summary>
    public static byte[] Write(ushort baseSequence, ReadOnlySpan<long> arrivalMicroseconds)
    {
        var count = arrivalMicroseconds.Length;
        if (count is 0 or > MaxPackets) throw new ArgumentOutOfRangeException(nameof(arrivalMicroseconds));
        long reference = -1;
        foreach (var arrival in arrivalMicroseconds)
            if (arrival >= 0) { reference = arrival / 1000; break; }
        var bitmap = (count + 7) / 8;
        var buffer = new byte[HeaderSize + bitmap + count * 3];
        buffer[0] = (byte)FrameType.TransportFeedback;
        buffer[1] = Version;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(2), baseSequence);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(4), (ushort)count);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(6), unchecked((uint)Math.Max(0, reference)));
        var offset = HeaderSize + bitmap;
        var previousTicks = Math.Max(0, reference) * 1000 / TickMicroseconds;
        for (var index = 0; index < count; index++)
        {
            var arrival = arrivalMicroseconds[index];
            if (arrival < 0) continue;
            buffer[HeaderSize + index / 8] |= (byte)(1 << (index % 8));
            var ticks = arrival / TickMicroseconds;
            var delta = Math.Clamp(ticks - previousTicks, short.MinValue, short.MaxValue);
            previousTicks += delta;
            if (delta is >= 0 and <= 254) buffer[offset++] = (byte)delta;
            else
            {
                buffer[offset++] = 0xFF;
                BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(offset), (short)delta);
                offset += 2;
            }
        }
        return buffer.AsSpan(0, offset).ToArray();
    }

    /// <summary>
    /// Decode a report into <paramref name="arrivalMicroseconds"/> (one entry per sequence from the base: relay clock in
    /// µs, at 250 µs resolution, or -1 for a message that did not arrive). False when it is malformed.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<byte> frame, out ushort baseSequence, List<long> arrivalMicroseconds)
    {
        baseSequence = 0;
        arrivalMicroseconds.Clear();
        if (frame.Length < HeaderSize || frame[0] != (byte)FrameType.TransportFeedback || frame[1] != Version) return false;
        baseSequence = BinaryPrimitives.ReadUInt16LittleEndian(frame[2..]);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(frame[4..]);
        if (count is 0 or > MaxPackets) return false;
        var reference = (long)BinaryPrimitives.ReadUInt32LittleEndian(frame[6..]);
        var bitmap = (count + 7) / 8;
        if (frame.Length < HeaderSize + bitmap) return false;
        var offset = HeaderSize + bitmap;
        var ticks = reference * 1000 / TickMicroseconds;
        for (var index = 0; index < count; index++)
        {
            if ((frame[HeaderSize + index / 8] & (1 << (index % 8))) == 0) { arrivalMicroseconds.Add(-1); continue; }
            if (offset >= frame.Length) { arrivalMicroseconds.Clear(); return false; }
            int delta = frame[offset++];
            if (delta == 0xFF)
            {
                if (offset + 2 > frame.Length) { arrivalMicroseconds.Clear(); return false; }
                delta = BinaryPrimitives.ReadInt16LittleEndian(frame[offset..]);
                offset += 2;
            }
            ticks += delta;
            arrivalMicroseconds.Add(Math.Max(0, ticks * TickMicroseconds));
        }
        return offset == frame.Length;
    }
}
