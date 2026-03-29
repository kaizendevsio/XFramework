namespace Bolt.Media;

/// <summary>
/// Delay-based congestion controller inspired by Google GCC (draft-ietf-rmcat-gcc).
///
/// Detects congestion BEFORE packet loss occurs by monitoring one-way delay variation.
/// Uses a Kalman-filter-style approach:
/// 1. Track inter-arrival time vs inter-departure time (one-way delay gradient)
/// 2. Smoothed gradient → overuse/underuse/normal state
/// 3. State drives AIMD bitrate adjustment
///
/// Designed to work alongside the loss-based AdaptiveBitrateController:
/// - This controller adjusts bitrate based on delay trends
/// - The loss-based controller adjusts based on actual packet loss
/// - The final bitrate is the minimum of both controllers' recommendations
/// </summary>
public sealed class DelayBasedController
{
    // ── Kalman filter state ──
    private double _estimatedOffset;   // Smoothed one-way delay gradient (ms)
    private double _offsetVariance = 1.0;
    private readonly double _processNoise = 1e-3;
    private readonly double _measurementNoise = 0.1;

    // ── Overuse detection ──
    private double _threshold = 12.5;     // Dynamic threshold (ms)
    private const double ThresholdMax = 600.0;
    private const double ThresholdMin = 6.0;
    private const double ThresholdGain = 4.0; // kU in the spec

    private int _overuseCount;
    private const int OveruseTimeThreshold = 10; // ticks before declaring overuse

    // ── Rate control ──
    private int _currentBitrateKbps;
    private int _minBitrateKbps;
    private int _maxBitrateKbps;
    private CongestionState _state = CongestionState.Hold;
    private long _lastUpdateTicks;
    private double _avgMaxBitrateKbps = -1;
    private double _varMaxBitrateKbps;

    // ── Frame timing ──
    private long _lastSendTimestamp;
    private long _lastRecvTimestamp;
    private bool _initialized;

    /// <summary>Current recommended bitrate from the delay-based controller.</summary>
    public int RecommendedBitrateKbps => _currentBitrateKbps;

    /// <summary>Current congestion state.</summary>
    public CongestionState State => _state;

    /// <summary>Fired when the controller recommends a bitrate change.</summary>
    public event Action<int>? OnBitrateChanged;

    public DelayBasedController(int initialBitrateKbps, bool isAudio)
    {
        _currentBitrateKbps = initialBitrateKbps;
        _minBitrateKbps = isAudio ? 16 : 100;
        _maxBitrateKbps = isAudio ? 256 : 10_000;
    }

    /// <summary>
    /// Record a frame's send timestamp and receive timestamp.
    /// Call this for every received frame to track delay variation.
    /// </summary>
    /// <param name="sendTimestamp">RTP-style timestamp from the frame header.</param>
    /// <param name="recvTimestampMs">Local receive time in milliseconds.</param>
    public void RecordFrame(uint sendTimestamp, long recvTimestampMs)
    {
        if (!_initialized)
        {
            _lastSendTimestamp = sendTimestamp;
            _lastRecvTimestamp = recvTimestampMs;
            _initialized = true;
            _lastUpdateTicks = Environment.TickCount64;
            return;
        }

        // One-way delay gradient: difference in inter-arrival vs inter-departure times
        var sendDelta = (long)sendTimestamp - _lastSendTimestamp;
        var recvDelta = recvTimestampMs - _lastRecvTimestamp;
        var delayGradient = recvDelta - sendDelta; // Positive = delay increasing

        _lastSendTimestamp = sendTimestamp;
        _lastRecvTimestamp = recvTimestampMs;

        // Kalman filter update
        UpdateKalmanFilter(delayGradient);

        // Overuse detection
        DetectOveruse();

        // Rate adaptation (not more than once per 250ms)
        var now = Environment.TickCount64;
        if (now - _lastUpdateTicks >= 250)
        {
            _lastUpdateTicks = now;
            UpdateRate();
        }
    }

    private void UpdateKalmanFilter(double measurement)
    {
        // Predict
        var predictedVariance = _offsetVariance + _processNoise;

        // Update
        var kalmanGain = predictedVariance / (predictedVariance + _measurementNoise);
        _estimatedOffset += kalmanGain * (measurement - _estimatedOffset);
        _offsetVariance = (1 - kalmanGain) * predictedVariance;
    }

    private void DetectOveruse()
    {
        if (_estimatedOffset > _threshold)
        {
            _overuseCount++;
            if (_overuseCount >= OveruseTimeThreshold)
                _state = CongestionState.Overuse;
        }
        else if (_estimatedOffset < -_threshold)
        {
            _overuseCount = 0;
            _state = CongestionState.Underuse;
        }
        else
        {
            _overuseCount = 0;
            _state = CongestionState.Normal;
        }

        // Adaptive threshold (per GCC spec)
        var absOffset = Math.Abs(_estimatedOffset);
        _threshold += (absOffset - _threshold) * (1.0 / (ThresholdGain * 1000));
        _threshold = Math.Clamp(_threshold, ThresholdMin, ThresholdMax);
    }

    private void UpdateRate()
    {
        int newBitrate;

        switch (_state)
        {
            case CongestionState.Overuse:
                // Multiplicative decrease (0.85x)
                newBitrate = (int)(_currentBitrateKbps * 0.85);
                newBitrate = Math.Max(newBitrate, _minBitrateKbps);

                // Track average max for recovery
                if (_avgMaxBitrateKbps < 0)
                    _avgMaxBitrateKbps = _currentBitrateKbps;
                else
                    _avgMaxBitrateKbps = 0.95 * _avgMaxBitrateKbps + 0.05 * _currentBitrateKbps;

                break;

            case CongestionState.Normal:
                // Additive increase
                var increase = Math.Max(1.0, 1000.0 / _currentBitrateKbps); // kbps per step
                newBitrate = _currentBitrateKbps + (int)increase;

                // Don't exceed the recent max unless we're significantly below
                if (_avgMaxBitrateKbps > 0 && newBitrate > _avgMaxBitrateKbps * 1.1)
                    newBitrate = (int)(_avgMaxBitrateKbps * 1.1);

                newBitrate = Math.Min(newBitrate, _maxBitrateKbps);
                break;

            case CongestionState.Underuse:
            default:
                return; // Hold — no change
        }

        if (newBitrate != _currentBitrateKbps)
        {
            _currentBitrateKbps = newBitrate;
            OnBitrateChanged?.Invoke(newBitrate);
        }
    }
}

/// <summary>
/// Congestion state as determined by delay-based analysis.
/// </summary>
public enum CongestionState
{
    /// <summary>Delay is stable — can increase bitrate (AIMD additive increase).</summary>
    Normal,
    /// <summary>Delay is growing — reduce bitrate (AIMD multiplicative decrease).</summary>
    Overuse,
    /// <summary>Delay is decreasing — hold current bitrate.</summary>
    Underuse,
    /// <summary>Initial state or waiting — no change.</summary>
    Hold,
}
