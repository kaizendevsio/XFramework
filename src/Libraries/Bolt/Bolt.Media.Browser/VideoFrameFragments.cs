using System.Buffers.Binary;

namespace Bolt.Media.Browser;

/// <summary>A reassembled encoded picture, ready for the decoder.</summary>
/// <param name="Layer">Temporal layer (0 = base, or a sender without layers).</param>
public readonly record struct VideoFramePayload(byte[] Data, uint TimestampMicroseconds, bool IsKeyframe, bool Discontinuity = false, uint FrameId = 0,
    int Layer = 0);

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
/// </summary>
public static class VideoFrameFragments
{
    /// <summary>Matches the SFrame session's plaintext limit.</summary>
    public const int MaxPlaintext = 4096;
    public const int HeaderSize = 12;
    public const int MaxPayload = MaxPlaintext - HeaderSize;
    /// <summary>96 fragments is ~392 KB: far above any sane 1080p keyframe, far below a memory problem.</summary>
    public const int MaxFragments = 96;
    private const byte Version = 0x10;
    private const byte KeyframeFlag = 0x01, LastFlag = 0x02;
    internal const byte LayerMask = 0x0C;
    internal const int LayerShift = 2;

    public static int FragmentCount(int encodedLength) => Math.Max(1, (encodedLength + MaxPayload - 1) / MaxPayload);

    /// <summary>
    /// Cut one encoded picture into wire fragments. Returns an empty list when the picture is
    /// larger than the reassembly bound: dropping it is correct, a partial picture is not.
    /// </summary>
    public static List<byte[]> Split(ReadOnlySpan<byte> encoded, uint frameId, uint timestampMicroseconds, bool isKeyframe, int layer = 0)
    {
        var layerBits = (byte)((isKeyframe ? 0 : Math.Clamp(layer, 0, 3)) << LayerShift);
        var count = FragmentCount(encoded.Length);
        if (encoded.Length == 0 || count > MaxFragments) return [];
        var fragments = new List<byte[]>(count);
        for (var index = 0; index < count; index++)
        {
            var offset = index * MaxPayload;
            var size = Math.Min(MaxPayload, encoded.Length - offset);
            var fragment = new byte[HeaderSize + size];
            fragment[0] = (byte)(Version | (isKeyframe ? KeyframeFlag : 0) | (index == count - 1 ? LastFlag : 0) | layerBits);
            fragment[1] = (byte)(count - 1);
            BinaryPrimitives.WriteUInt16LittleEndian(fragment.AsSpan(2), (ushort)index);
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
        public uint Timestamp;
        public bool Keyframe;
        public int Layer;
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

    /// <summary>Feed one decrypted fragment. Returns the picture once its last missing piece lands.</summary>
    public VideoFramePayload? Add(ReadOnlySpan<byte> fragment)
    {
        if (fragment.Length <= VideoFrameFragments.HeaderSize ||
            fragment.Length > VideoFrameFragments.MaxPlaintext ||
            (fragment[0] & VersionMask) != Version) return null;
        var total = fragment[1] + 1;
        var index = BinaryPrimitives.ReadUInt16LittleEndian(fragment[2..]);
        var frameId = BinaryPrimitives.ReadUInt32LittleEndian(fragment[4..]);
        var timestamp = BinaryPrimitives.ReadUInt32LittleEndian(fragment[8..]);
        var payload = fragment[VideoFrameFragments.HeaderSize..];
        if (total > VideoFrameFragments.MaxFragments || index >= total) return null;
        // Only the final fragment may be short; anything else is a truncated or forged split.
        if (index != total - 1 && payload.Length != VideoFrameFragments.MaxPayload) return null;
        // A picture already handed to the decoder must not be rebuilt from replayed fragments.
        if (hasCompleted && unchecked(frameId - lastCompleted) is 0 or > 0x8000_0000u) return null;

        if (!pending.TryGetValue(frameId, out var slot))
        {
            if (pending.Count >= MaxPending) DropOldest();
            pending[frameId] = slot = new Pending { Parts = new byte[total][], Total = total, Order = ++sequence };
        }
        else if (slot.Total != total) { pending.Remove(frameId); Incomplete++; return null; }

        if (slot.Parts[index] is not null) return null;
        slot.Parts[index] = payload.ToArray();
        slot.Received++; slot.Bytes += payload.Length;
        slot.Timestamp = timestamp;
        if ((fragment[0] & KeyframeFlag) != 0) slot.Keyframe = true;
        slot.Layer = (fragment[0] & VideoFrameFragments.LayerMask) >> VideoFrameFragments.LayerShift;
        if (slot.Layer > 0) Layered = true;
        if (slot.Received != slot.Total) return null;

        var data = new byte[slot.Bytes];
        var offset = 0;
        foreach (var part in slot.Parts) { part!.CopyTo(data, offset); offset += part.Length; }
        pending.Remove(frameId);
        var gap = hasCompleted && unchecked(frameId - lastCompleted) != 1;
        // With temporal layers a gap is a policy drop, and what arrived is decodable (see the class remarks).
        var discontinuity = gap && (!Layered || localLoss);
        if (slot.Keyframe || discontinuity) localLoss = false;
        lastCompleted = frameId; hasCompleted = true;
        // Fragments of older pictures still in flight are now useless; their picture can never be shown in order.
        foreach (var stale in pending.Where(x => unchecked(x.Key - frameId) > 0x8000_0000u).Select(x => x.Key).ToArray())
        { pending.Remove(stale); Incomplete++; }
        return new(data, slot.Timestamp, slot.Keyframe, discontinuity, frameId, slot.Keyframe ? 0 : slot.Layer);
    }

    public void Reset() { pending.Clear(); hasCompleted = false; Incomplete = 0; localLoss = false; }

    private void DropOldest()
    {
        var oldest = pending.OrderBy(x => x.Value.Order).First().Key;
        pending.Remove(oldest);
        Incomplete++;
    }
}
