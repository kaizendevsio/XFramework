using Bolt.Protocol.Transport;

namespace Bolt.Server;

/// <summary>
/// The last video frames the relay received from one sender's stream, kept so a receiver on a datagram path can get a
/// lost frame again from the relay (one hop) instead of from the sender (the whole path, twice). Frames are kept as
/// they arrived, still SFrame-encrypted end to end; the relay resends bytes it already forwarded and can read nothing
/// more than before. Only frames that fit one datagram are kept: a larger one only ever travels on a WebSocket, which
/// does not lose it. Bounded by slot count and by age.
/// </summary>
internal sealed class RelayRetransmitCache
{
    public const int DefaultCapacity = 512;
    /// <summary>Beyond this no receiver can still use a frame: its recovery window is at most 1.5 s.</summary>
    public const int DefaultMaxAgeMs = 2_000;

    private readonly (uint Sequence, long At, byte[]? Frame, bool Used)[] _slots;
    private readonly int _maxFrameBytes, _maxAgeMs;
    private readonly object _sync = new();
    private uint _newest;
    private bool _hasNewest;

    public RelayRetransmitCache(int capacity = DefaultCapacity, int maxFrameBytes = RtcDefaults.MaxMessageBytes, int maxAgeMs = DefaultMaxAgeMs)
    {
        _slots = new (uint, long, byte[]?, bool)[Math.Max(16, capacity)];
        _maxFrameBytes = maxFrameBytes;
        _maxAgeMs = maxAgeMs;
    }

    /// <summary>
    /// Record a frame that arrived from the sender. Returns <see cref="Arrival.Late"/> when it is older than the newest
    /// one (a retransmission the sender made because a receiver asked), <see cref="Arrival.Duplicate"/> when it was
    /// already here, and <see cref="Arrival.InOrder"/> otherwise.
    /// </summary>
    public Arrival Store(uint sequence, ReadOnlySpan<byte> frame, long now)
    {
        lock (_sync)
        {
            var slot = (int)(sequence % (uint)_slots.Length);
            if (_slots[slot].Used && _slots[slot].Sequence == sequence && now - _slots[slot].At <= _maxAgeMs)
                return Arrival.Duplicate;
            var late = _hasNewest && unchecked(_newest - sequence) is > 0 and < 0x8000_0000u;
            _slots[slot] = (sequence, now, frame.Length <= _maxFrameBytes ? frame.ToArray() : null, true);
            if (!late) { _newest = sequence; _hasNewest = true; }
            return late ? Arrival.Late : Arrival.InOrder;
        }
    }

    /// <summary>What the relay knows of one frame.</summary>
    public Holding Lookup(uint sequence, long now, out byte[]? frame)
    {
        lock (_sync)
        {
            frame = null;
            if (!_hasNewest) return Holding.NeverSeen;
            var behind = unchecked(_newest - sequence);
            if (behind >= 0x8000_0000u) return Holding.NeverSeen; // Not sent yet as far as the relay knows.
            var slot = _slots[(int)(sequence % (uint)_slots.Length)];
            if (slot.Used && slot.Sequence == sequence)
            {
                if (now - slot.At > _maxAgeMs || slot.Frame is null) return Holding.Gone;
                frame = slot.Frame;
                return Holding.Held;
            }
            // Within the window but never arrived: lost on the sender's uplink.
            return behind < (uint)_slots.Length ? Holding.NeverSeen : Holding.Gone;
        }
    }

    public enum Arrival { InOrder, Late, Duplicate }

    public enum Holding
    {
        /// <summary>The relay has the frame and can resend it.</summary>
        Held,
        /// <summary>The relay never got it (the sender's uplink lost it): only the sender can resend it.</summary>
        NeverSeen,
        /// <summary>Too old, or too large for a datagram: nobody will resend it.</summary>
        Gone,
    }
}

/// <summary>
/// Which video frames of each stream one receiver's connection has actually been sent (from its media lanes, or as a
/// retransmission). The video lane is first-in first-out, so a frame older than the newest one sent that was not sent
/// itself never will be: the relay dropped it on purpose (its layer policy or a congested queue), or never had it.
/// </summary>
internal sealed class VideoSendLedger
{
    private const int Window = 1024;
    private const int MaxStreams = 32;
    private readonly object _sync = new();
    private readonly Dictionary<Guid, Stream> _streams = [];

    private sealed class Stream
    {
        public readonly bool[] Sent = new bool[Window];
        public readonly uint[] Sequences = new uint[Window];
        public uint Newest;
        public bool HasNewest;
    }

    public enum Status
    {
        /// <summary>It was sent to this receiver.</summary>
        Sent,
        /// <summary>Later frames were sent and this one was not: it will not be sent from the lanes.</summary>
        Skipped,
        /// <summary>Newer than anything sent: it may still be queued.</summary>
        Pending,
        /// <summary>Too far back to say.</summary>
        Unknown,
    }

    public void Record(Guid streamId, uint sequence)
    {
        lock (_sync)
        {
            if (!_streams.TryGetValue(streamId, out var stream))
            {
                if (_streams.Count >= MaxStreams) _streams.Clear();
                _streams[streamId] = stream = new Stream();
            }
            var slot = (int)(sequence % Window);
            stream.Sent[slot] = true;
            stream.Sequences[slot] = sequence;
            if (!stream.HasNewest || unchecked(sequence - stream.Newest) is > 0 and < 0x8000_0000u)
            {
                stream.Newest = sequence;
                stream.HasNewest = true;
            }
        }
    }

    public Status Lookup(Guid streamId, uint sequence)
    {
        lock (_sync)
        {
            if (!_streams.TryGetValue(streamId, out var stream) || !stream.HasNewest) return Status.Pending;
            var behind = unchecked(stream.Newest - sequence);
            if (behind >= 0x8000_0000u) return Status.Pending;
            if (behind >= Window) return Status.Unknown;
            var slot = (int)(sequence % Window);
            return stream.Sent[slot] && stream.Sequences[slot] == sequence ? Status.Sent : Status.Skipped;
        }
    }

    public void Forget(Guid streamId)
    {
        lock (_sync) _streams.Remove(streamId);
    }
}
