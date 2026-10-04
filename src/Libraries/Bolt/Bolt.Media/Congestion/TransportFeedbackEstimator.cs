namespace Bolt.Media.Congestion;

/// <summary>
/// What the relay's transport feedback says about this sender's uplink (the leg from this device to the relay), as the
/// rate controller reads it.
/// </summary>
/// <param name="ReceivedAtMs">When the latest report arrived.</param>
/// <param name="QueueDelayMs">One-way delay of the latest messages above its recent minimum: the queue on the uplink.</param>
/// <param name="TrendMsPerSecond">Slope of that delay over the last groups of messages (GCC's trendline): positive while a queue builds.</param>
/// <param name="LossFraction">Messages lost on the uplink over about the last second.</param>
/// <param name="DeliveredKbps">What reached the relay over about the last second, framing included.</param>
public readonly record struct TransportSignal(long ReceivedAtMs, int QueueDelayMs, double TrendMsPerSecond, double LossFraction, int DeliveredKbps);

/// <summary>
/// The sender's half of transport-wide delivery feedback: the send time and size of every stamped datagram, matched
/// against the arrival times the relay reports (<see cref="Bolt.Protocol.TransportFeedbackCodec"/>).
///
/// Delay. Each message's one-way delay is arrival minus send. The two clocks share no epoch, so only its rise matters:
/// the queuing delay is the latest delay above its minimum over two alternating 10 s windows. Messages are grouped
/// into 5 ms send bursts, as GCC does, and a least-squares line through the last 20 groups' delays against their
/// arrival time gives the trend: a queue that is building shows as a positive slope before the delay is large.
///
/// Loss. Messages reported missing over the last second, against all reported.
///
/// Throughput. Bytes that arrived over the last second, by their arrival times: what the uplink actually carried.
/// </summary>
public sealed class TransportFeedbackEstimator
{
    private const int Window = 4096;
    private const int MinWindowMs = 10_000;
    private const int GroupUs = 5_000;
    private const int TrendGroups = 20;
    private const int RateWindowMs = 1_000;

    private readonly (ushort Sequence, long SentUs, int Bytes, bool Used)[] _sent = new (ushort, long, int, bool)[Window];
    private readonly object _sync = new();
    private readonly Queue<(long ArrivalUs, int Bytes)> _arrived = new();
    private readonly Queue<(long AtMs, bool Received)> _outcomes = new();
    private readonly List<(double ArrivalMs, double DelayMs)> _groups = [];
    private double _currentMin = double.MaxValue, _previousMin = double.MaxValue;
    private long _windowStartedAt = long.MinValue;
    private long _groupStartUs = long.MinValue;
    private double _groupDelaySum, _groupArrivalMs;
    private int _groupCount;
    private double _latestDelay;
    private TransportSignal? _signal;

    /// <summary>Record one stamped message as it is handed to the channel.</summary>
    public void OnSent(ushort sequence, int bytes, long nowUs)
    {
        lock (_sync) _sent[sequence % Window] = (sequence, nowUs, bytes, true);
    }

    /// <summary>
    /// One report: <paramref name="arrivalUs"/> holds, from <paramref name="baseSequence"/> on, the relay's arrival time of
    /// each message (its clock, µs) or a negative value for one that did not arrive.
    /// </summary>
    public void OnFeedback(ushort baseSequence, IReadOnlyList<long> arrivalUs, long nowMs)
    {
        lock (_sync)
        {
            var matched = false;
            double delaySum = 0;
            var delays = 0;
            for (var index = 0; index < arrivalUs.Count; index++)
            {
                var sequence = unchecked((ushort)(baseSequence + index));
                var slot = _sent[sequence % Window];
                if (!slot.Used || slot.Sequence != sequence) continue;
                _sent[sequence % Window].Used = false;
                matched = true;
                var arrival = arrivalUs[index];
                _outcomes.Enqueue((nowMs, arrival >= 0));
                if (arrival < 0) continue;
                _arrived.Enqueue((arrival, slot.Bytes));
                var delay = (arrival - slot.SentUs) / 1000.0;
                ObserveDelay(delay, nowMs);
                delaySum += delay;
                delays++;
                Group(slot.SentUs, arrival, delay);
            }
            if (!matched) return;
            // The queue now: the report's mean delay (one late message is jitter, not a queue) above the floor.
            if (delays > 0) _latestDelay = delaySum / delays;
            while (_outcomes.Count > 0 && nowMs - _outcomes.Peek().AtMs > RateWindowMs) _outcomes.Dequeue();
            var latestArrival = _arrived.Count > 0 ? _arrived.Last().ArrivalUs : 0;
            while (_arrived.Count > 0 && latestArrival - _arrived.Peek().ArrivalUs > RateWindowMs * 1000L) _arrived.Dequeue();

            var lost = _outcomes.Count(x => !x.Received);
            var loss = _outcomes.Count == 0 ? 0 : (double)lost / _outcomes.Count;
            var delivered = 0;
            if (_arrived.Count >= 2)
            {
                var spanUs = Math.Max(1, latestArrival - _arrived.Peek().ArrivalUs);
                // The first message only opens the interval; the bytes after it arrived within it.
                var bytes = _arrived.Sum(x => (long)x.Bytes) - _arrived.Peek().Bytes;
                delivered = spanUs >= 200_000 ? (int)Math.Round(bytes * 8.0 / (spanUs / 1000.0)) : 0;
            }
            var floor = Math.Min(_currentMin, _previousMin);
            var queue = (int)Math.Clamp(_latestDelay - floor, 0, ushort.MaxValue);
            _signal = new TransportSignal(nowMs, queue, Trend(), loss, delivered);
        }
    }

    /// <summary>The estimate after the latest report, or null before the first.</summary>
    public TransportSignal? Signal { get { lock (_sync) return _signal; } }

    public void Reset()
    {
        lock (_sync)
        {
            Array.Clear(_sent);
            _arrived.Clear();
            _outcomes.Clear();
            _groups.Clear();
            _currentMin = _previousMin = double.MaxValue;
            _windowStartedAt = _groupStartUs = long.MinValue;
            _groupCount = 0;
            _signal = null;
        }
    }

    private void ObserveDelay(double delay, long nowMs)
    {
        if (_windowStartedAt == long.MinValue) _windowStartedAt = nowMs;
        if (nowMs - _windowStartedAt >= MinWindowMs)
        {
            _previousMin = _currentMin;
            _currentMin = double.MaxValue;
            _windowStartedAt = nowMs;
        }
        _currentMin = Math.Min(_currentMin, delay);
    }

    /// <summary>Messages sent within 5 ms of each other form one group, represented by its mean delay and last arrival.</summary>
    private void Group(long sentUs, long arrivalUs, double delay)
    {
        if (_groupStartUs != long.MinValue && sentUs - _groupStartUs > GroupUs) CloseGroup();
        if (_groupCount == 0) _groupStartUs = sentUs;
        _groupDelaySum += delay;
        _groupArrivalMs = Math.Max(_groupArrivalMs, arrivalUs / 1000.0);
        _groupCount++;
    }

    private void CloseGroup()
    {
        if (_groupCount == 0) return;
        _groups.Add((_groupArrivalMs, _groupDelaySum / _groupCount));
        if (_groups.Count > TrendGroups) _groups.RemoveAt(0);
        _groupDelaySum = 0;
        _groupArrivalMs = 0;
        _groupCount = 0;
        _groupStartUs = long.MinValue;
    }

    /// <summary>Least-squares slope of group delay against group arrival, in ms of delay per second.</summary>
    private double Trend()
    {
        if (_groups.Count < 4) return 0;
        double n = _groups.Count, sx = 0, sy = 0, sxx = 0, sxy = 0;
        var origin = _groups[0].ArrivalMs;
        foreach (var (arrival, delay) in _groups)
        {
            var x = (arrival - origin) / 1000.0;
            sx += x; sy += delay; sxx += x * x; sxy += x * delay;
        }
        var denominator = n * sxx - sx * sx;
        return denominator <= 1e-9 ? 0 : (n * sxy - sx * sy) / denominator;
    }
}
