namespace Bolt.Protocol;

/// <summary>
/// Remembers which of a stream's recent sequence numbers arrived, so a second copy of a frame (audio
/// redundancy on a datagram path, or a frame racing on two paths during a switch) is dropped before
/// anyone forwards or decrypts it, and so the loss of the path can be measured.
///
/// Frames may arrive out of order: anything within <see cref="Size"/> of the newest is accepted once.
/// A frame much older than that means the sender restarted its sequence, which starts the window over.
/// </summary>
public sealed class SequenceWindow
{
    public const int Size = 1024;
    private const int Words = Size / 64;

    private readonly object _sync = new();
    private readonly ulong[] _seen = new ulong[Words];
    private bool _any;
    private uint _highest;
    private uint _sampledHighest;
    private long _receivedSinceSample;

    /// <summary>Record <paramref name="sequence"/>. False for a duplicate, which the caller drops.</summary>
    public bool TryMark(uint sequence)
    {
        lock (_sync)
        {
            if (!_any)
            {
                Start(sequence);
                return true;
            }
            var ahead = unchecked(sequence - _highest);
            if (ahead == 0)
                return false;
            if (ahead < 0x8000_0000u)
            {
                // Newer: clear the slots the window slides over, then take the new head.
                var clear = Math.Min(ahead, (uint)Size);
                for (uint offset = 1; offset <= clear; offset++)
                    Clear(unchecked(_highest + offset));
                _highest = sequence;
                Set(sequence);
                _receivedSinceSample++;
                return true;
            }
            var behind = unchecked(_highest - sequence);
            if (behind >= Size)
            {
                // Far behind the head: the sender started over. Old history no longer means anything.
                Array.Clear(_seen);
                Start(sequence);
                return true;
            }
            if (IsSet(sequence))
                return false;
            Set(sequence);
            _receivedSinceSample++;
            return true;
        }
    }

    /// <summary>
    /// Frames expected (by how far the newest sequence advanced) and frames that arrived since the previous
    /// sample. Loss is the difference; a late frame of the previous interval counts in this one.
    /// </summary>
    public (long Expected, long Received) Sample()
    {
        lock (_sync)
        {
            if (!_any)
                return (0, 0);
            var expected = (long)unchecked(_highest - _sampledHighest);
            var received = _receivedSinceSample;
            _sampledHighest = _highest;
            _receivedSinceSample = 0;
            return (expected, Math.Min(received, expected));
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            Array.Clear(_seen);
            _any = false;
            _receivedSinceSample = 0;
        }
    }

    private void Start(uint sequence)
    {
        _any = true;
        _highest = sequence;
        _sampledHighest = unchecked(sequence - 1);
        _receivedSinceSample = 1;
        Set(sequence);
    }

    private void Set(uint sequence) => _seen[(sequence / 64) % Words] |= 1UL << (int)(sequence % 64);
    private void Clear(uint sequence) => _seen[(sequence / 64) % Words] &= ~(1UL << (int)(sequence % 64));
    private bool IsSet(uint sequence) => (_seen[(sequence / 64) % Words] & (1UL << (int)(sequence % 64))) != 0;
}

/// <summary>Loss in thousandths over a sample, 0 when nothing was expected.</summary>
public static class LossMath
{
    public static int Permille(long expected, long received) =>
        expected <= 0 ? 0 : (int)Math.Clamp((expected - received) * 1000 / expected, 0, 1000);
}
