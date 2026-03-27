using System.Buffers.Binary;

namespace Bolt.Client.Media;

/// <summary>
/// Helper for QUIC unreliable datagram support.
/// Datagrams are used for drop-eligible media frames (audio, video delta)
/// when the QUIC connection supports them.
/// </summary>
public static class QuicDatagramHelper
{
    /// <summary>
    /// Maximum datagram payload size (conservative, below typical MTU).
    /// Frames larger than this must use reliable streams.
    /// </summary>
    public const int MaxDatagramSize = 1200;

    /// <summary>
    /// Check if a media frame should use unreliable datagram transport.
    /// Keyframes always use reliable streams.
    /// </summary>
    public static bool ShouldUseDatagram(byte flags, int payloadLength)
    {
        var isKeyframe = (flags & 0x01) != 0;
        if (isKeyframe) return false;
        if (payloadLength > MaxDatagramSize) return false;
        var isDropEligible = (flags & 0x40) != 0;
        return isDropEligible;
    }

    /// <summary>
    /// Fragment a large payload into datagram-sized chunks.
    /// Each fragment has a 4-byte sub-header: [2:fragmentOffset][2:totalFragments]
    /// </summary>
    public static List<byte[]> Fragment(ReadOnlyMemory<byte> payload, int maxChunkSize = MaxDatagramSize - 4)
    {
        var fragments = new List<byte[]>();
        var totalFragments = (ushort)((payload.Length + maxChunkSize - 1) / maxChunkSize);

        for (ushort i = 0; i < totalFragments; i++)
        {
            var offset = i * maxChunkSize;
            var length = Math.Min(maxChunkSize, payload.Length - offset);
            var fragment = new byte[4 + length];

            BinaryPrimitives.WriteUInt16LittleEndian(fragment, i);
            BinaryPrimitives.WriteUInt16LittleEndian(fragment.AsSpan(2), totalFragments);
            payload.Slice(offset, length).CopyTo(fragment.AsMemory(4));

            fragments.Add(fragment);
        }

        return fragments;
    }

    /// <summary>
    /// Reassemble fragments back into the original payload.
    /// Returns null if not all fragments have been received.
    /// </summary>
    public static byte[]? Reassemble(Dictionary<ushort, byte[]> fragments, ushort totalFragments)
    {
        if (fragments.Count < totalFragments) return null;

        var totalSize = 0;
        for (ushort i = 0; i < totalFragments; i++)
        {
            if (!fragments.TryGetValue(i, out var frag)) return null;
            totalSize += frag.Length - 4;
        }

        var result = new byte[totalSize];
        var pos = 0;
        for (ushort i = 0; i < totalFragments; i++)
        {
            var frag = fragments[i];
            var dataLen = frag.Length - 4;
            frag.AsSpan(4, dataLen).CopyTo(result.AsSpan(pos));
            pos += dataLen;
        }

        return result;
    }

    /// <summary>
    /// Parse the fragment sub-header from a received datagram.
    /// </summary>
    public static (ushort FragmentIndex, ushort TotalFragments, ReadOnlyMemory<byte> Data) ParseFragment(ReadOnlyMemory<byte> datagram)
    {
        var span = datagram.Span;
        var index = BinaryPrimitives.ReadUInt16LittleEndian(span);
        var total = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(2));
        return (index, total, datagram.Slice(4));
    }
}
