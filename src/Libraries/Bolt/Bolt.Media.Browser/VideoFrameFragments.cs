using System.Buffers.Binary;

namespace Bolt.Media.Browser;

/// <summary>A reassembled encoded picture, ready for the decoder.</summary>
/// <param name="Layer">Temporal layer (0 = base, or a sender without layers).</param>
/// <param name="Orientation">
/// How the receiver turns the decoded picture: quarter turns clockwise (bits 0-1), then a horizontal flip (bit 2), as
/// VideoFrame.rotation and VideoFrame.flip define them. 0 is upright.
/// </param>
public readonly record struct VideoFramePayload(byte[] Data, uint TimestampMicroseconds, bool IsKeyframe, bool Discontinuity = false, uint FrameId = 0,
    int Layer = 0, int Orientation = 0);

/// <summary>
/// Splits an encoded picture into SFrame-sized pieces and puts it back together.
///
/// The SFrame session accepts at most 4096 plaintext bytes per operation - a bound chosen for Opus
/// packets - while a 1080p keyframe is tens of kilobytes. Rather than widen the crypto bound (and
/// with it the relay's per-frame memory exposure) every picture is cut into fragments that each go
/// through the same authenticated encryption as an audio packet.
///
/// The fragment header travels inside the SFrame plaintext, so the relay cannot see or forge a
/// picture boundary, a keyframe flag, a temporal layer or a timestamp; tampering fails the AEAD tag
/// like any other payload edit. The relay gets its own copy of the layer in the clear MediaFrame
/// flags, which only decides what it forwards: receivers decode by the authenticated one.
///
/// Header byte 0 is the version (high nibble), the keyframe (0x01) and last-fragment (0x02) bits and
/// the temporal layer (bits 2-3). Receivers that predate layers ignore bits 2-3.
///
/// Version 2 differs only in bytes 2-3: the fragment index is byte 2 alone (a picture has at most 256 fragments, so
/// the index's high byte was always zero) and byte 3 is the picture's orientation (bits 0-2; the rest must be zero).
/// A sender uses it only for a picture that is not upright, and only when every receiver announced
/// <see cref="CallMediaFormat.Oriented"/> in its authenticated key envelope: an older receiver drops version 2. Being
/// in the plaintext, the orientation is encrypted and authenticated with the picture, so a relay can neither read
/// nor turn it.
///
/// The fragment size depends on the sender's path: 4 KB on a WebSocket, about 800 bytes on a data
/// channel, where each fragment must fit one unretransmitted datagram. Within one picture every
/// fragment but the last has the same size, and a receiver checks exactly that.
/// </summary>
public static class VideoFrameFragments
{
    /// <summary>Matches the SFrame session's plaintext limit.</summary>
    public const int MaxPlaintext = 4096;
    public const int HeaderSize = 12;
    public const int MaxPayload = MaxPlaintext - HeaderSize;
    /// <summary>The header counts at most 256 fragments (byte 1 is the count minus one).</summary>
    public const int MaxFragments = 256;
    /// <summary>~392 KB per picture: far above any sane 1080p keyframe, far below a memory problem.</summary>
    public const int MaxPictureBytes = 96 * MaxPayload;
    /// <summary>Smallest fragment payload a sender uses, so no picture is cut into hundreds of tiny pieces.</summary>
    public const int MinPayload = 256;
    /// <summary>What SFrame adds to a fragment before the first one tells (header, tag and the bound context).</summary>
    public const int DefaultEncryptionOverhead = 320;
    /// <summary>Room for the authenticated context to grow (its sequence and timestamp digits).</summary>
    public const int ContextSlack = 24;
    private const byte Version = 0x10;
    internal const byte OrientedVersion = 0x20;
    /// <summary>Quarter turns (bits 0-1) and the flip (bit 2).</summary>
    public const int OrientationMask = 0x07;
    private const byte KeyframeFlag = 0x01, LastFlag = 0x02;
    internal const byte LayerMask = 0x0C;
    internal const int LayerShift = 2;

    public static int FragmentCount(int encodedLength) => FragmentCount(encodedLength, MaxPayload);

    public static int FragmentCount(int encodedLength, int payload) => Math.Max(1, (encodedLength + payload - 1) / payload);

    /// <summary>
    /// Cut one encoded picture into wire fragments. Returns an empty list when the picture is
    /// larger than the reassembly bound: dropping it is correct, a partial picture is not.
    /// </summary>
    /// <param name="orientation">The picture's orientation code (see <see cref="VideoFramePayload.Orientation"/>); 0 keeps version 1.</param>
    public static List<byte[]> Split(ReadOnlySpan<byte> encoded, uint frameId, uint timestampMicroseconds, bool isKeyframe, int layer = 0,
        int payload = MaxPayload, int orientation = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(orientation & ~OrientationMask, 0, nameof(orientation));
        payload = Math.Clamp(payload, MinPayload, MaxPayload);
        var layerBits = (byte)((isKeyframe ? 0 : Math.Clamp(layer, 0, 3)) << LayerShift);
        var count = FragmentCount(encoded.Length, payload);
        if (encoded.Length == 0 || count > MaxFragments || encoded.Length > MaxPictureBytes) return [];
        var fragments = new List<byte[]>(count);
        for (var index = 0; index < count; index++)
        {
            var offset = index * payload;
            var size = Math.Min(payload, encoded.Length - offset);
            var fragment = new byte[HeaderSize + size];
            fragment[0] = (byte)((orientation == 0 ? Version : OrientedVersion) | (isKeyframe ? KeyframeFlag : 0) |
                                 (index == count - 1 ? LastFlag : 0) | layerBits);
            fragment[1] = (byte)(count - 1);
            if (orientation == 0) BinaryPrimitives.WriteUInt16LittleEndian(fragment.AsSpan(2), (ushort)index);
            else { fragment[2] = (byte)index; fragment[3] = (byte)orientation; }
            BinaryPrimitives.WriteUInt32LittleEndian(fragment.AsSpan(4), frameId);
            BinaryPrimitives.WriteUInt32LittleEndian(fragment.AsSpan(8), timestampMicroseconds);
            encoded.Slice(offset, size).CopyTo(fragment.AsSpan(HeaderSize));
            fragments.Add(fragment);
        }
        return fragments;
    }
}

/// <summary>
/// Reassembles one remote sender's fragments. Bounded on purpose: a sender that never finishes a
/// picture, or that interleaves many, can only ever hold a few hundred kilobytes here.
///
/// A missing picture normally breaks the stream until a keyframe. A stream with temporal layers is
/// different: the relay and the sender only ever drop a layer-L picture together with every later
/// picture of layer L or above until the next base picture, so whatever still arrives refers only to
/// pictures that arrived. There a gap is not a discontinuity, unless this receiver lost something itself
/// (<see cref="MarkLocalLoss"/>), which no drop policy covered.
/// </summary>
public sealed class VideoFrameAssembler
{
    private const int MaxPending = 3;
    private const byte VersionMask = 0xF0, Version = 0x10;
    private const byte KeyframeFlag = 0x01;

    private sealed class Pending
    {
        public byte[]?[] Parts = [];
        public int Received, Total, Bytes;
        /// <summary>Payload size of this picture's non-final fragments, once one has arrived.</summary>
        public int FragmentSize;
        public uint Timestamp;
        public bool Keyframe;
        public int Layer, Orientation;
        public long Order;
    }

    private readonly Dictionary<uint, Pending> pending = [];
    private long sequence;
    private uint lastCompleted;
    private bool hasCompleted;
    private bool localLoss;

    /// <summary>The sender has marked at least one enhancement-layer picture.</summary>
    public bool Layered { get; private set; }

    /// <summary>This receiver dropped fragments itself; the next gap is a real break in the stream.</summary>
    public void MarkLocalLoss() => localLoss = true;

    /// <summary>Frames discarded because a fragment never arrived. Surfaces as a loss signal.</summary>
    public int Incomplete { get; private set; }

    /// <summary>A fragment header that passed the checks every fragment must pass on its own.</summary>
    internal readonly record struct FragmentHeader(int Total, int Index, uint FrameId, uint Timestamp, bool Keyframe, int Layer, int Orientation);

    /// <summary>
    /// Parse one fragment's header with the checks that need no other fragment: version, sizes, the count, that a
    /// non-final fragment is not short, and that a version 2 orientation byte has no unknown bits. Shared with
    /// <see cref="VideoRecoveryBuffer"/>.
    /// </summary>
    internal static bool TryParse(ReadOnlySpan<byte> fragment, out FragmentHeader header)
    {
        header = default;
        if (fragment.Length <= VideoFrameFragments.HeaderSize || fragment.Length > VideoFrameFragments.MaxPlaintext) return false;
        var version = fragment[0] & VersionMask;
        if (version != Version && version != VideoFrameFragments.OrientedVersion) return false;
        var total = fragment[1] + 1;
        int index, orientation = 0;
        if (version == Version) index = BinaryPrimitives.ReadUInt16LittleEndian(fragment[2..]);
        else
        {
            index = fragment[2];
            orientation = fragment[3];
            if ((orientation & ~VideoFrameFragments.OrientationMask) != 0) return false;
        }
        var payload = fragment.Length - VideoFrameFragments.HeaderSize;
        if (total > VideoFrameFragments.MaxFragments || index >= total) return false;
        // Fragments of one picture share one size, set by the sender's path; only the final one may be short.
        // Anything else is a truncated or forged split. The whole picture stays within the reassembly bound.
        if (index != total - 1 && payload < VideoFrameFragments.MinPayload) return false;
        if ((long)(total - 1) * VideoFrameFragments.MinPayload > VideoFrameFragments.MaxPictureBytes) return false;
        header = new FragmentHeader(total, index, BinaryPrimitives.ReadUInt32LittleEndian(fragment[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(fragment[8..]), (fragment[0] & KeyframeFlag) != 0,
            (fragment[0] & VideoFrameFragments.LayerMask) >> VideoFrameFragments.LayerShift, orientation);
        return true;
    }

    /// <summary>Feed one decrypted fragment. Returns the picture once its last missing piece lands.</summary>
    public VideoFramePayload? Add(ReadOnlySpan<byte> fragment)
    {
        if (!TryParse(fragment, out var header)) return null;
        var (total, index, frameId, timestamp) = (header.Total, header.Index, header.FrameId, header.Timestamp);
        var payload = fragment[VideoFrameFragments.HeaderSize..];
        // A picture already handed to the decoder must not be rebuilt from replayed fragments.
        if (hasCompleted && unchecked(frameId - lastCompleted) is 0 or > 0x8000_0000u) return null;

        if (!pending.TryGetValue(frameId, out var slot))
        {
            if (pending.Count >= MaxPending) DropOldest();
            pending[frameId] = slot = new Pending { Parts = new byte[total][], Total = total, Orientation = header.Orientation, Order = ++sequence };
        }
        // One picture has one fragment count and one orientation; a fragment that disagrees is not part of it.
        else if (slot.Total != total || slot.Orientation != header.Orientation) { pending.Remove(frameId); Lost(); return null; }

        if (slot.Parts[index] is not null) return null;
        var size = index != total - 1 ? payload.Length : 0;
        if (size != 0 && slot.FragmentSize != 0 && size != slot.FragmentSize) { pending.Remove(frameId); Lost(); return null; }
        if (size != 0) slot.FragmentSize = size;
        if (slot.FragmentSize != 0 && index == total - 1 && payload.Length > slot.FragmentSize) { pending.Remove(frameId); Lost(); return null; }
        if (slot.Bytes + payload.Length > VideoFrameFragments.MaxPictureBytes) { pending.Remove(frameId); Lost(); return null; }
        slot.Parts[index] = payload.ToArray();
        slot.Received++; slot.Bytes += payload.Length;
        slot.Timestamp = timestamp;
        if (header.Keyframe) slot.Keyframe = true;
        slot.Layer = header.Layer;
        if (slot.Layer > 0) Layered = true;
        if (slot.Received != slot.Total) return null;
        // A one-fragment picture has no size to check; otherwise the last part must not exceed the others.
        if (slot.Total > 1 && slot.Parts[^1]!.Length > slot.FragmentSize) { pending.Remove(frameId); Lost(); return null; }

        var data = new byte[slot.Bytes];
        var offset = 0;
        foreach (var part in slot.Parts) { part!.CopyTo(data, offset); offset += part.Length; }
        pending.Remove(frameId);
        // Fragments of older pictures still in flight are now useless; their picture can never be shown in order.
        // Part of a picture arrived and the rest never did: that is loss on this path (a relay drops whole pictures),
        // and this very picture may refer to it, unless it is a keyframe.
        foreach (var stale in pending.Where(x => unchecked(x.Key - frameId) > 0x8000_0000u).Select(x => x.Key).ToArray())
        {
            pending.Remove(stale);
            Incomplete++;
            if (!slot.Keyframe) localLoss = true;
        }
        var gap = hasCompleted && unchecked(frameId - lastCompleted) != 1;
        // With temporal layers a gap is a policy drop, and what arrived is decodable (see the class remarks).
        var discontinuity = gap && (!Layered || localLoss);
        if (slot.Keyframe || discontinuity) localLoss = false;
        lastCompleted = frameId; hasCompleted = true;
        return new(data, slot.Timestamp, slot.Keyframe, discontinuity, frameId, slot.Keyframe ? 0 : slot.Layer, slot.Orientation);
    }

    public void Reset() { pending.Clear(); hasCompleted = false; Incomplete = 0; localLoss = false; }

    private void DropOldest()
    {
        var oldest = pending.OrderBy(x => x.Value.Order).First().Key;
        pending.Remove(oldest);
        Lost();
    }

    /// <summary>A partly received picture was given up: whatever referred to it cannot decode, so the next gap is a break.</summary>
    private void Lost()
    {
        Incomplete++;
        localLoss = true;
    }
}
