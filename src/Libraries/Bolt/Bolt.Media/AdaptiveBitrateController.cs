using Bolt.Client;
using Bolt.Protocol;
using Bolt.Protocol.Buffers;

namespace Bolt.Media;

/// <summary>
/// Adaptive bitrate controller that works on both sides of a media stream:
/// - Receiver side: tracks frame reception metrics (loss, jitter) and sends periodic MediaFeedback.
/// - Sender side: processes incoming feedback and fires events to adjust encoding parameters.
/// </summary>
public sealed class AdaptiveBitrateController : IAsyncDisposable
{
    private readonly BoltConnection _connection;
    private readonly Guid _streamId;
    private readonly bool _isAudio;

    // ── Sender-side state ──
    private int _currentBitrateKbps;
    private int _minBitrateKbps;
    private int _maxBitrateKbps;

    // ── Receiver-side state ──
    private uint _highestSeqReceived;
    private uint _expectedSeq;
    private uint _cumulativeLost;
    private long _lastArrivalTicks;
    private double _jitterSmoothed; // Smoothed jitter in ticks (EWA)

    // ── Feedback loop ──
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private bool _disposed;

    /// <summary>Current target bitrate in kbps (sender side).</summary>
    public int CurrentBitrateKbps => _currentBitrateKbps;

    /// <summary>Fired on the sender side when feedback indicates bitrate should change.</summary>
    public event Action<int>? OnBitrateChanged;

    /// <summary>Fired on the sender side when receiver reports excessive loss and needs a keyframe.</summary>
    public event Action? OnKeyframeRequested;

    public AdaptiveBitrateController(BoltConnection connection, Guid streamId, int initialBitrateKbps, bool isAudio)
    {
        _connection = connection;
        _streamId = streamId;
        _isAudio = isAudio;
        _currentBitrateKbps = initialBitrateKbps;
        _minBitrateKbps = isAudio ? 16 : 100;
        _maxBitrateKbps = isAudio ? 256 : 10_000;
    }

    /// <summary>
    /// Record a received frame's sequence number (receiver side).
    /// Tracks loss and inter-arrival jitter for feedback reports.
    /// </summary>
    public void RecordFrameReceived(uint seq)
    {
        var now = Environment.TickCount64;

        if (seq > _highestSeqReceived)
        {
            // Detect gaps
            if (_expectedSeq > 0 && seq > _expectedSeq)
            {
                var gap = seq - _expectedSeq;
                _cumulativeLost += gap;
            }

            _highestSeqReceived = seq;
            _expectedSeq = seq + 1;
        }

        // Inter-arrival jitter (RFC 3550 smoothing)
        if (_lastArrivalTicks > 0)
        {
            var interArrival = now - _lastArrivalTicks;
            var deviation = Math.Abs(interArrival - (_isAudio ? 20 : 33)); // expected interval ms
            _jitterSmoothed += (deviation - _jitterSmoothed) / 16.0;
        }

        _lastArrivalTicks = now;
    }

    /// <summary>
    /// Process feedback received from the remote receiver (sender side).
    /// Adjusts bitrate: decrease 25% on loss/jitter, increase 10% when stable.
    /// </summary>
    public void ProcessFeedback(MediaFeedbackData feedback)
    {
        switch (feedback.QualityHint)
        {
            case QualityHint.Decrease:
                // Decrease by 25%
                var decreased = (int)(_currentBitrateKbps * 0.75);
                _currentBitrateKbps = Math.Max(decreased, _minBitrateKbps);
                OnBitrateChanged?.Invoke(_currentBitrateKbps);
                break;

            case QualityHint.Increase:
                // Increase by 10%
                var increased = (int)(_currentBitrateKbps * 1.10);
                _currentBitrateKbps = Math.Min(increased, _maxBitrateKbps);
                OnBitrateChanged?.Invoke(_currentBitrateKbps);
                break;

            case QualityHint.KeyframeNeeded:
                OnKeyframeRequested?.Invoke();
                break;

            case QualityHint.Maintain:
            default:
                break;
        }
    }

    /// <summary>
    /// Start the periodic feedback loop (receiver side).
    /// Sends MediaFeedback every 250ms with current reception metrics.
    /// </summary>
    public void Start()
    {
        if (_loopTask != null) return;

        _loopCts = new CancellationTokenSource();
        _loopTask = Task.Run(() => FeedbackLoopAsync(_loopCts.Token));
    }

    private async Task FeedbackLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(ct))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                var jitterX100 = (uint)(_jitterSmoothed * 100);
                var hint = DetermineQualityHint();

                var writer = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteMediaFeedback(writer, _streamId, _highestSeqReceived, _cumulativeLost, jitterX100, 0, hint);
                await _connection.SendAsync(writer.WrittenMemory, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Connection may be closing; swallow and let loop exit naturally
            }
        }
    }

    private QualityHint DetermineQualityHint()
    {
        // High loss rate -> decrease or request keyframe
        if (_highestSeqReceived > 0)
        {
            var lossRate = (double)_cumulativeLost / (_highestSeqReceived + 1);

            if (lossRate > 0.10)
                return QualityHint.KeyframeNeeded;

            if (lossRate > 0.03 || _jitterSmoothed > 50)
                return QualityHint.Decrease;
        }

        // Low jitter and low loss -> can increase
        if (_jitterSmoothed < 10 && _cumulativeLost == 0 && _highestSeqReceived > 100)
            return QualityHint.Increase;

        return QualityHint.Maintain;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_loopCts != null)
        {
            await _loopCts.CancelAsync();
            if (_loopTask != null)
            {
                try { await _loopTask; }
                catch (OperationCanceledException) { }
            }
            _loopCts.Dispose();
        }
    }
}
