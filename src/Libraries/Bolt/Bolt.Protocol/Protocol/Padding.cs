using System.Buffers.Binary;

namespace Bolt.Protocol;

/// <summary>
/// <see cref="FrameType.Padding"/>: a probe message on the datagram path, [1:type] [1:flags] [1:step] [1:reserved]
/// [4:index] then zero bytes. A participant stamps it like any other message (<see cref="TransportSequenceCodec"/>), so
/// the relay's transport feedback times its arrival on the uplink; with <see cref="Echo"/> set the relay sends it back
/// (<see cref="Echoed"/>), so the participant times the downlink too. The payload is zeros: it carries no media, no
/// keys and nothing from any other participant, and is never forwarded to anyone else.
/// </summary>
public static class PaddingCodec
{
    public const int HeaderSize = 8;
    /// <summary>Participant to relay: send this message back to me.</summary>
    public const byte Echo = 0x01;
    /// <summary>Relay to participant: this is the echo of one of your probe messages.</summary>
    public const byte Echoed = 0x02;

    public static int Write(Span<byte> destination, byte flags, byte step, uint index)
    {
        if (destination.Length < HeaderSize) throw new ArgumentException("Too small for a padding message.", nameof(destination));
        destination[0] = (byte)FrameType.Padding;
        destination[1] = flags;
        destination[2] = step;
        destination[3] = 0;
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], index);
        destination[HeaderSize..].Clear();
        return destination.Length;
    }

    public static bool TryRead(ReadOnlySpan<byte> frame, out byte flags, out byte step, out uint index)
    {
        flags = step = 0;
        index = 0;
        if (frame.Length < HeaderSize || frame[0] != (byte)FrameType.Padding) return false;
        flags = frame[1];
        step = frame[2];
        index = BinaryPrimitives.ReadUInt32LittleEndian(frame[4..]);
        return true;
    }

    /// <summary>The echo of <paramref name="probe"/>: the same bytes, marked as coming back.</summary>
    public static void MarkEchoed(Span<byte> probe) => probe[1] = Echoed;
}
