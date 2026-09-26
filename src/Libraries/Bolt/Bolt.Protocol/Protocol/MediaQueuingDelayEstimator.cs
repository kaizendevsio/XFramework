namespace Bolt.Protocol;

/// <summary>
/// How far a media stream's one-way delay sits above its recent minimum: the queue the path added, including
/// buffers neither end can see (a kernel send buffer, the radio, a proxy). The relay runs one per stream for the
/// sender's uplink; a receiver runs one per stream for the whole path.
///
/// Transit is arrival time minus the clear media timestamp. Its absolute value is meaningless (the two clocks
/// share no epoch) but its growth is exactly the queuing the path added. The minimum is kept over two
/// alternating 10 s windows: a path whose base delay rises settles on the new floor within 20 s, and a standing
/// queue stays visible for at least 10 s.
/// </summary>
public sealed class MediaQueuingDelayEstimator
{
    /// <summary>Length of each of the two minimum windows.</summary>
    public const int WindowMs = 10_000;
    /// <summary>A timestamp jump larger than this (either way) is a restart or a wrap, not delay.</summary>
    private const double MaxStepMs = 30_000;

    private bool _started;
    private uint _lastTimestamp;
    private double _mediaMs;
    private double _currentMin = double.MaxValue, _previousMin = double.MaxValue;
    private long _windowStartedAt;
    private double _lastTransit;

    /// <summary>Queuing delay estimate after the latest observation, in milliseconds.</summary>
    public int DelayMs { get; private set; }

    /// <param name="timestamp">Clear media timestamp.</param>
    /// <param name="clockKhz">Media clock in kHz: 90 for video, 48 for audio.</param>
    /// <param name="nowMs">Arrival time.</param>
    public void Observe(uint timestamp, int clockKhz, long nowMs)
    {
        if (_started)
        {
            var step = unchecked((int)(timestamp - _lastTimestamp)) / (double)clockKhz;
            if (step == 0) return;
            if (Math.Abs(step) > MaxStepMs) Restart(nowMs);
            else _mediaMs += step;
        }
        else
        {
            Restart(nowMs);
        }
        _lastTimestamp = timestamp;

        if (nowMs - _windowStartedAt >= WindowMs)
        {
            _previousMin = _currentMin;
            _currentMin = double.MaxValue;
            _windowStartedAt = nowMs;
        }
        _lastTransit = nowMs - _mediaMs;
        _currentMin = Math.Min(_currentMin, _lastTransit);
        var floor = Math.Min(_currentMin, _previousMin);
        DelayMs = (int)Math.Clamp(_lastTransit - floor, 0, ushort.MaxValue);
    }

    private void Restart(long nowMs)
    {
        _started = true;
        _mediaMs = 0;
        _currentMin = _previousMin = double.MaxValue;
        _windowStartedAt = nowMs;
        DelayMs = 0;
    }
}
