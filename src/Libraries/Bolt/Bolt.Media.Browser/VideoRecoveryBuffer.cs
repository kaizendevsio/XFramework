using System.Buffers.Binary;

namespace Bolt.Media.Browser;

/// <summary>
/// One remote video stream's reassembly with loss recovery, for a receiver on a datagram path whose relay resends
/// lost frames (<see cref="Bolt.Protocol.MediaTransportFeatures.Nack"/>).
///
/// Without recovery (<see cref="RecoveryMs"/> 0, the WebSocket path) it is exactly <see cref="VideoFrameAssembler"/>:
/// TCP loses nothing, a gap is a relay's deliberate drop, and nothing is worth asking for again.
///
/// With recovery:
/// <list type="bullet">
/// <item>Every missing MediaFrame sequence number (a gap, or a fragment a partly received picture still lacks) is asked
/// for after a short reorder wait, again after a round trip if it is still missing, at most three times, and only while
/// it can still arrive inside the recovery window (about one and a half round trips, plus the time a fragment lost on
/// the sender's uplink takes to come back from the sender; at least 350 ms, at most 1.5 s).</item>
/// <item>Pictures go to the decoder in order. A complete picture waits behind an older one that is still being
/// recovered, because it may refer to it; nothing waits behind a missing top-layer picture, which nothing refers to.</item>
/// <item>A picture that cannot be recovered in time is given up the cheapest way the reference structure allows: a lost
/// enhancement-layer picture costs only the pictures that refer to it (later pictures of a higher layer, until one of
/// its own layer or lower), with no keyframe; a lost base-layer picture, or a picture of unknown layer (nothing of it
/// arrived), makes the next picture a discontinuity, so the decoder restarts on a keyframe it asks for.</item>
/// <item>Frames the relay declines (it dropped them on purpose, or no longer has them) are not waited for.</item>
/// </list>
///
/// Like the assembler it is bounded (pictures, bytes, missing numbers) and validates every fragment the same way: a
/// sender or relay that never completes pictures can only make this receiver give them up. Not thread-safe; the caller
/// serializes Push, Poll and Declined.
/// </summary>
public sealed class VideoRecoveryBuffer
{
    /// <summary>Wait this long for a reordered frame before asking for it.</summary>
    public const int ReorderMs = 15;
    public const int MaxTries = 3;
    public const int MaxRecoveryMs = 1_500;
    /// <summary>
    /// What a fragment lost on the sender's uplink adds: only the sender has it, and it learns of the loss from the
    /// relay's transport feedback (every 100 ms) before it sends it again across both legs; room for a second try if
    /// that copy is lost too.
    /// </summary>
    public const int UplinkRepairMs = 250;
    public const int MinRecoveryMs = 350;

    /// <summary>
    /// How long the stream must have been quiet before the rest of an incomplete picture counts as lost: a sender's pacer
    /// and its transport pause between fragments (audio goes first, the channel drains), but not for two round trips.
    /// </summary>
    private int TailQuietMs => Math.Clamp(RttMs * 2, 40, 300);
    private const int MaxPictures = 48;
    private const long MaxBufferedBytes = 2 * 1024 * 1024;
    private const int MaxMissing = 1024;
    /// <summary>A forward jump this large is a restarted sender, not loss.</summary>
    private const uint MaxGap = 512;

    private readonly VideoFrameAssembler _plain = new();
    private readonly Dictionary<uint, Picture> _pictures = [];
    private readonly Dictionary<uint, Missing> _missing = [];
    private long _bytes;
    private long _lastArrivalAt;
    private uint _highest;
    private bool _hasHighest;
    private uint _lastReleased, _lastHandled;
    private bool _hasReleased, _hasHandled;
    private bool _localLoss, _layered;
    private int _topLayer;
    private int? _skipAbove;

    private sealed class Picture
    {
        public uint FrameId, FirstSequence, Timestamp;
        public int Total, Received, Bytes, FragmentSize, Layer, Orientation;
        public byte[]?[] Parts = [];
        public bool Keyframe, Complete, Lost;
        /// <summary>When its latest fragment arrived: a picture still arriving is not late, however long it is.</summary>
        public long LastSeenAt;
    }

    private sealed class Missing(long since)
    {
        public long Since { get; } = since;
        public long LastAskedAt;
        public int Tries;
    }

    /// <summary>Recovery window in milliseconds; 0 turns recovery off (plain reassembly).</summary>
    public int RecoveryMs { get; private set; }

    /// <summary>The round trip NACKs are paced by.</summary>
    public int RttMs { get; private set; } = 200;

    /// <summary>Sequence numbers asked for, recovered after asking, given up, and declined by the relay.</summary>
    public int Nacked { get; private set; }
    public int Recovered { get; private set; }
    public int Abandoned { get; private set; }
    public int Declined { get; private set; }
    /// <summary>Pictures not shown because they referred to a lost enhancement picture (no keyframe was needed).</summary>
    public int Skipped { get; private set; }
    /// <summary>Pictures given up because a fragment never arrived.</summary>
    public int Incomplete => _incomplete + _plain.Incomplete;
    private int _incomplete;
    /// <summary>Fragments pushed (after decryption), and whole pictures handed on to the decoder.</summary>
    public long Fragments { get; private set; }
    public long Pictures { get; private set; }

    public bool Layered => RecoveryMs == 0 ? _plain.Layered : _layered;

    /// <summary>
    /// Recovery for a path with this round trip, or off. Returns true when recovery was switched on or off: the two modes
    /// share no state, so the caller asks for a keyframe to start the decoder cleanly.
    /// </summary>
    public bool Configure(bool recover, int rttMs)
    {
        RttMs = Math.Clamp(rttMs, 1, 5_000);
        var window = recover ? Math.Clamp(RttMs * 3 / 2 + 50 + UplinkRepairMs, MinRecoveryMs, MaxRecoveryMs) : 0;
        var switched = (window == 0) != (RecoveryMs == 0);
        RecoveryMs = window;
        if (switched) Reset();
        return switched;
    }

    /// <summary>This device dropped fragments itself: whatever is missing next was no policy's choice.</summary>
    public void MarkLocalLoss()
    {
        if (RecoveryMs == 0) _plain.MarkLocalLoss();
        else _localLoss = true;
    }

    public void Reset()
    {
        _plain.Reset();
        _pictures.Clear();
        _missing.Clear();
        _bytes = 0;
        _hasHighest = _hasReleased = _hasHandled = false;
        _localLoss = _layered = false;
        _topLayer = 0;
        _skipAbove = null;
    }

    /// <summary>Feed one decrypted fragment with its MediaFrame sequence number. Pictures ready to decode are added to <paramref name="ready"/>.</summary>
    public void Push(uint sequence, ReadOnlySpan<byte> fragment, long nowMs, List<VideoFramePayload> ready)
    {
        Fragments++;
        if (RecoveryMs == 0)
        {
            if (_plain.Add(fragment) is { } picture) { ready.Add(picture); Pictures++; }
            return;
        }
        if (!VideoFrameAssembler.TryParse(fragment, out var header)) return;
        _lastArrivalAt = nowMs;

        if (_missing.Remove(sequence, out var asked) && asked.Tries > 0) Recovered++;
        if (!_hasHighest)
        {
            _highest = sequence;
            _hasHighest = true;
        }
        else if (Newer(sequence, _highest))
        {
            var gap = unchecked(sequence - _highest);
            if (gap <= MaxGap)
                for (uint offset = 1; offset < gap; offset++) AddMissing(unchecked(_highest + offset), nowMs);
            else
            {
                _missing.Clear();
                _localLoss = true;
            }
            _highest = sequence;
        }

        // A picture already decoded, skipped or given up must not be rebuilt from late or replayed fragments.
        if (_hasHandled && !Newer(header.FrameId, _lastHandled)) return;
        var first = unchecked(sequence - (uint)header.Index);
        if (!_pictures.TryGetValue(header.FrameId, out var slot))
        {
            if (_pictures.Count >= MaxPictures) LoseOldest();
            _pictures[header.FrameId] = slot = new Picture
            {
                FrameId = header.FrameId, FirstSequence = first, Total = header.Total, Orientation = header.Orientation,
                Parts = new byte[header.Total][], LastSeenAt = nowMs,
            };
        }
        else if (slot.Total != header.Total || slot.FirstSequence != first || slot.Orientation != header.Orientation)
        {
            Lose(slot);
            Release(ready);
            return;
        }
        if (slot.Lost || slot.Complete || slot.Parts[header.Index] is not null) return;

        // The assembler's integrity rules: one fragment size per picture, a short last one, the picture bound.
        var payload = fragment[VideoFrameFragments.HeaderSize..];
        var last = header.Index == header.Total - 1;
        var size = last ? 0 : payload.Length;
        if ((size != 0 && slot.FragmentSize != 0 && size != slot.FragmentSize) ||
            (last && slot.FragmentSize != 0 && payload.Length > slot.FragmentSize) ||
            slot.Bytes + payload.Length > VideoFrameFragments.MaxPictureBytes)
        {
            Lose(slot);
            Release(ready);
            return;
        }
        if (size != 0) slot.FragmentSize = size;
        slot.LastSeenAt = nowMs;
        slot.Parts[header.Index] = payload.ToArray();
        slot.Received++;
        slot.Bytes += payload.Length;
        _bytes += payload.Length;
        slot.Timestamp = header.Timestamp;
        if (header.Keyframe) slot.Keyframe = true;
        slot.Layer = header.Layer;
        if (header.Layer > 0) _layered = true;
        _topLayer = Math.Max(_topLayer, header.Layer);
        if (slot.Received == slot.Total)
        {
            if (slot.Total > 1 && slot.Parts[^1]!.Length > slot.FragmentSize) Lose(slot);
            else slot.Complete = true;
        }
        while (_bytes > MaxBufferedBytes && _pictures.Count > 0) LoseOldest();
        Release(ready);
    }

    /// <summary>
    /// Time passes: ask for what is missing (added to <paramref name="nacks"/>), give up what can no longer arrive in
    /// time, and release pictures that no longer wait for anything.
    /// </summary>
    public void Poll(long nowMs, List<VideoFramePayload> ready, List<uint> nacks)
    {
        if (RecoveryMs == 0) return;

        // The tail of a picture no later frame revealed as missing. Fragments arrive at the rate the sender's link
        // carries them, so a large picture takes as long as it takes (a 1440p keyframe through a 4.7 Mbit/s uplink, about
        // 350 ms): what has not arrived yet is not missing while the stream is still arriving. Only once it has gone
        // quiet is the rest of an incomplete picture asked for, and timed from then.
        if (_hasHighest && nowMs - _lastArrivalAt >= TailQuietMs)
            foreach (var picture in _pictures.Values)
            {
                if (picture.Complete || picture.Lost) continue;
                for (var index = 0; index < picture.Total; index++)
                {
                    var sequence = unchecked(picture.FirstSequence + (uint)index);
                    if (picture.Parts[index] is null && Newer(sequence, _highest)) AddMissing(sequence, _lastArrivalAt);
                }
            }

        foreach (var (sequence, missing) in _missing.ToArray())
        {
            var age = nowMs - missing.Since;
            var owner = Owner(sequence);
            if (owner is { Lost: true }) { _missing.Remove(sequence); continue; }
            if (age >= RecoveryMs)
            {
                _missing.Remove(sequence);
                Abandoned++;
                // A fragment of a picture we know: give that picture up. Nothing of it arrived: it may have been a base
                // picture, so the next gap is a break.
                if (owner is not null) Lose(owner);
                else _localLoss = true;
                continue;
            }
            var due = missing.Tries == 0 || nowMs - missing.LastAskedAt >= RttMs * 6 / 5 + 10;
            if (age >= ReorderMs && missing.Tries < MaxTries && age + RttMs <= RecoveryMs && due)
            {
                nacks.Add(sequence);
                missing.Tries++;
                missing.LastAskedAt = nowMs;
                Nacked++;
            }
        }

        // A picture that has made no progress for well past the window (its missing numbers were bounded away): give it up.
        foreach (var picture in _pictures.Values)
            if (!picture.Complete && !picture.Lost && nowMs - picture.LastSeenAt >= RecoveryMs + RttMs)
                Lose(picture);

        Release(ready);
    }

    /// <summary>The relay will not resend these: stop waiting for them.</summary>
    public void Decline(ReadOnlySpan<uint> sequences, List<VideoFramePayload> ready)
    {
        if (RecoveryMs == 0) return;
        foreach (var sequence in sequences)
        {
            if (!_missing.Remove(sequence)) continue;
            Declined++;
            // Half a picture the relay will not finish is lost; a whole picture it dropped on purpose is a policy gap.
            if (Owner(sequence) is { Complete: false } owner) Lose(owner);
        }
        Release(ready);
    }

    private void Release(List<VideoFramePayload> ready)
    {
        while (Oldest() is { } picture)
        {
            if (picture.Lost)
            {
                Remove(picture);
                GiveUp(picture);
                continue;
            }
            if (!picture.Complete)
            {
                // Nothing refers to a top-layer picture: the pictures after it need not wait for it.
                if (_layered && picture.Layer > 0 && picture.Layer >= _topLayer && _pictures.Values.Any(x => x.Complete && x != picture))
                {
                    Lose(picture);
                    continue;
                }
                break;
            }
            // A frame older than this picture is still being recovered: it may be the picture this one refers to.
            if (_missing.Keys.Any(sequence => Newer(picture.FirstSequence, sequence))) break;
            Remove(picture);
            Emit(picture, ready);
        }
    }

    private void Emit(Picture picture, List<VideoFramePayload> ready)
    {
        Handled(picture.FrameId);
        if (_skipAbove is { } skip)
        {
            if (!picture.Keyframe && picture.Layer > skip)
            {
                // It refers to the lost enhancement picture: not shown, and no keyframe needed for it.
                Skipped++;
                return;
            }
            _skipAbove = null;
        }
        var gap = _hasReleased && unchecked(picture.FrameId - _lastReleased) != 1;
        // The first picture after a reset (recovery switched on or off mid-stream, a decoder restart) follows pictures
        // this buffer no longer knows: unless it is a keyframe, the decoder must not take it as a continuation.
        var discontinuity = (!_hasReleased && !picture.Keyframe) || (gap && (!_layered || _localLoss));
        if (picture.Keyframe || discontinuity) _localLoss = false;
        var data = new byte[picture.Bytes];
        var offset = 0;
        foreach (var part in picture.Parts) { part!.CopyTo(data, offset); offset += part.Length; }
        _lastReleased = picture.FrameId;
        _hasReleased = true;
        Pictures++;
        ready.Add(new VideoFramePayload(data, picture.Timestamp, picture.Keyframe, discontinuity, picture.FrameId,
            picture.Keyframe ? 0 : picture.Layer, picture.Orientation));
    }

    /// <summary>The cheapest recovery the reference structure allows for a picture that will not be shown.</summary>
    private void GiveUp(Picture picture)
    {
        Handled(picture.FrameId);
        _incomplete++;
        if (picture.Keyframe || picture.Layer == 0 || !_layered) _localLoss = true;
        else _skipAbove = Math.Min(_skipAbove ?? picture.Layer, picture.Layer);
    }

    private void Lose(Picture picture)
    {
        if (picture.Lost) return;
        picture.Lost = true;
        picture.Complete = false;
        for (var index = 0; index < picture.Total; index++)
            _missing.Remove(unchecked(picture.FirstSequence + (uint)index));
    }

    private void LoseOldest()
    {
        // Over a bound: the oldest picture goes, complete or not (a complete one was waiting for an older loss).
        if (Oldest() is not { } oldest) return;
        Lose(oldest);
        Remove(oldest);
        GiveUp(oldest);
    }

    private void Remove(Picture picture)
    {
        _pictures.Remove(picture.FrameId);
        _bytes -= picture.Bytes;
    }

    private void Handled(uint frameId)
    {
        if (!_hasHandled || Newer(frameId, _lastHandled)) { _lastHandled = frameId; _hasHandled = true; }
    }

    private Picture? Oldest()
    {
        Picture? oldest = null;
        foreach (var picture in _pictures.Values)
            if (oldest is null || Newer(oldest.FirstSequence, picture.FirstSequence)) oldest = picture;
        return oldest;
    }

    private Picture? Owner(uint sequence)
    {
        foreach (var picture in _pictures.Values)
            if (unchecked(sequence - picture.FirstSequence) < (uint)picture.Total) return picture;
        return null;
    }

    private void AddMissing(uint sequence, long since)
    {
        if (_missing.Count < MaxMissing) _missing.TryAdd(sequence, new Missing(since));
    }

    private static bool Newer(uint a, uint b) => unchecked(a - b) is > 0 and < 0x8000_0000u;
}
