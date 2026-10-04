using System.Diagnostics;
using Bolt.Protocol;

namespace Bolt.Server;

/// <summary>
/// Arrival times of one participant's transport-sequenced datagrams (<see cref="FrameType.TransportSequenced"/>), for
/// the periodic <see cref="FrameType.TransportFeedback"/> report back to it. Each report covers every sequence number
/// since the previous one; a message that arrives after its number was reported missing stays missing. The relay
/// learns nothing from this it did not already see (sizes and timing of what it forwards).
/// </summary>
internal sealed class TransportFeedbackRecorder
{
    private const int Window = TransportFeedbackCodec.MaxPackets;
    private readonly (ushort Sequence, long ArrivalUs, bool Used)[] _slots = new (ushort, long, bool)[Window];
    private readonly object _sync = new();
    private ushort _next, _highest;
    private bool _started;
    private int _pending;

    /// <summary>A monotonic clock in microseconds, the relay's side of every report.</summary>
    public static long NowMicroseconds() => (long)(Stopwatch.GetTimestamp() * (1_000_000.0 / Stopwatch.Frequency));

    public void Record(ushort sequence, long arrivalUs)
    {
        lock (_sync)
        {
            if (!_started)
            {
                _started = true;
                _next = _highest = sequence;
            }
            else if (Older(sequence, _next)) return; // Already reported missing.
            _slots[sequence % Window] = (sequence, arrivalUs, true);
            if (Older(_highest, sequence)) _highest = sequence;
            _pending++;
        }
    }

    /// <summary>The report of everything since the last one, or null when nothing arrived.</summary>
    public byte[]? TryBuild()
    {
        lock (_sync)
        {
            if (_pending == 0) return null;
            _pending = 0;
            var count = unchecked((ushort)(_highest - _next)) + 1;
            if (count > Window)
            {
                _next = unchecked((ushort)(_highest - Window + 1));
                count = Window;
            }
            var arrivals = new long[count];
            for (var index = 0; index < count; index++)
            {
                var sequence = unchecked((ushort)(_next + index));
                var slot = _slots[sequence % Window];
                arrivals[index] = slot.Used && slot.Sequence == sequence ? slot.ArrivalUs : -1;
                _slots[sequence % Window].Used = false;
            }
            var report = TransportFeedbackCodec.Write(_next, arrivals);
            _next = unchecked((ushort)(_highest + 1));
            return report;
        }
    }

    private static bool Older(ushort a, ushort b) => unchecked((ushort)(b - a)) is > 0 and < 0x8000;
}
