using Bolt.Protocol;

namespace Bolt.Media.Congestion;

/// <summary>
/// The remote half of a sender's congestion signals: the relay's reports and the receivers' delay reports for
/// this sender's own streams, kept per media kind and aggregated to the worst receiver.
/// </summary>
public sealed class SendPathSignals
{
    private readonly object _sync = new();
    private (MediaCongestionData Report, long At)? _relayVideo, _relayAudio;
    private bool _relayDropping, _relayBaseLost;
    private readonly List<(long At, bool Video, int DelayMs, int Kbps)> _feedback = [];
    private ReceiverSignal? _lastReceiver;
    private const int FeedbackWindowMs = 1_000;
    /// <summary>A receiver that went quiet is still reported, as it last was, for this long (see <see cref="SendRateController"/>).</summary>
    public const int SilentReceiverMs = 15_000;

    public void Clear()
    {
        lock (_sync)
        {
            _relayVideo = _relayAudio = null;
            _relayDropping = _relayBaseLost = false;
            _feedback.Clear();
            _lastReceiver = null;
        }
    }

    public void OnCongestionReport(in MediaCongestionData report, bool video, long nowMs)
    {
        lock (_sync)
        {
            if (video) _relayVideo = (report, nowMs);
            else _relayAudio = (report, nowMs);
            if ((report.Flags & MediaCongestionFlags.Dropping) != 0) _relayDropping = true;
            if ((report.Flags & MediaCongestionFlags.BaseLayerLost) != 0) _relayBaseLost = true;
        }
    }

    public void OnReceiverFeedback(in MediaFeedbackData feedback, bool video, long nowMs)
    {
        if (!feedback.HasDelayReport) return;
        lock (_sync)
        {
            _feedback.Add((nowMs, video, feedback.QueueDelayMs, (int)Math.Min(feedback.ReceivedKbps, int.MaxValue)));
            _feedback.RemoveAll(x => nowMs - x.At > FeedbackWindowMs);
        }
    }

    /// <summary>
    /// The latest signals as the controller reads them. The relay's per-stream capacity shares add up to this
    /// sender's share of the worst receiver; a stream the relay did not find limited is counted at what it sends.
    /// A drop report is consumed once.
    /// </summary>
    public (RelaySignal? Relay, ReceiverSignal? Receiver) Take(long nowMs, int sentVideoKbps, int sentAudioKbps, int freshMs = 1_500)
    {
        lock (_sync)
        {
            RelaySignal? relay = null;
            var video = _relayVideo is { } v && nowMs - v.At <= freshMs ? v : ((MediaCongestionData, long)?)null;
            var audio = _relayAudio is { } a && nowMs - a.At <= freshMs ? a : ((MediaCongestionData, long)?)null;
            if (video is not null || audio is not null)
            {
                int queue = 0, uplink = 0, capacity = 0;
                var limited = false;
                long at = 0;
                foreach (var (report, received, sent) in new[] { (video, true, sentVideoKbps), (audio, false, sentAudioKbps) })
                {
                    if (report is not { } r) continue;
                    at = Math.Max(at, r.Item2);
                    queue = Math.Max(queue, r.Item1.QueueDelayMs);
                    uplink = Math.Max(uplink, r.Item1.UplinkDelayMs);
                    if ((r.Item1.Flags & MediaCongestionFlags.Limited) != 0 && r.Item1.AllowedKbps > 0)
                    {
                        limited = true;
                        capacity += (int)Math.Min(r.Item1.AllowedKbps, int.MaxValue);
                    }
                    else capacity += sent;
                }
                relay = new RelaySignal(at, queue, uplink, limited ? capacity : 0, _relayDropping, _relayBaseLost);
                _relayDropping = _relayBaseLost = false;
            }

            ReceiverSignal? receiver = null;
            _feedback.RemoveAll(x => nowMs - x.At > FeedbackWindowMs);
            if (_feedback.Count > 0)
            {
                var delay = _feedback.Max(x => x.DelayMs);
                // Several receivers may report on one stream: the least any of them got is what the path carries.
                var videoKbps = _feedback.Where(x => x.Video).Select(x => x.Kbps).DefaultIfEmpty(-1).Min();
                var audioKbps = _feedback.Where(x => !x.Video).Select(x => x.Kbps).DefaultIfEmpty(-1).Min();
                var received = (videoKbps >= 0 ? videoKbps : sentVideoKbps) + (audioKbps >= 0 ? audioKbps : sentAudioKbps);
                receiver = _lastReceiver = new ReceiverSignal(_feedback.Max(x => x.At), delay, received);
            }
            // Receivers that stopped reporting are not fine: their feedback rides the same congested path back.
            else if (_lastReceiver is { } last && nowMs - last.ReceivedAtMs <= SilentReceiverMs)
                receiver = last;
            return (relay, receiver);
        }
    }
}

/// <summary>What one tick of <see cref="SendRateLoop"/> asks the host to do.</summary>
/// <param name="Decision">The controller's allocation.</param>
/// <param name="Video">New encoder setting (size, frame rate, bitrate), or null to keep the current one.</param>
/// <param name="SuspendVideo">Video must stand down now.</param>
/// <param name="ResumeVideo">Video may come back now, at <paramref name="Video"/>.</param>
/// <param name="AudioKbps">New Opus rate, or null to keep it.</param>
/// <param name="Pacer">What the sender's own queues saw.</param>
/// <param name="Relay">The relay signal the decision used, if any was fresh.</param>
/// <param name="Receiver">The receiver signal the decision used, if any was fresh.</param>
public readonly record struct SendRateTick(
    SendRateDecision Decision,
    VideoSetting? Video,
    bool SuspendVideo,
    bool ResumeVideo,
    int? AudioKbps,
    MediaSendPacerSample Pacer,
    RelaySignal? Relay = null,
    ReceiverSignal? Receiver = null);

/// <summary>
/// Couples one sender's pacer, rate controller and picture ladder. The host calls <see cref="Tick"/> every
/// <see cref="IntervalMs"/> and applies what it returns to the real encoders; the browser client and the network
/// harness both drive the same loop.
/// </summary>
public sealed class SendRateLoop
{
    public const int IntervalMs = 250;
    /// <summary>Encoder backlog windows before this device's own speed caps the picture.</summary>
    public const int CpuStrainedAfter = 2;
    /// <summary>Calm windows before a CPU cap is lifted by one rung.</summary>
    public const int CpuRelaxAfter = 120;

    private readonly SendPathSignals _signals;
    private int _audioKbps;
    private bool _suspended;
    private int _cpuStrained, _cpuCalm;

    public SendRateLoop(MediaSendPacer pacer, SendRateController controller, VideoRateLadder ladder, SendPathSignals? signals = null)
    {
        Pacer = pacer;
        Controller = controller;
        Ladder = ladder;
        _signals = signals ?? new SendPathSignals();
        _audioKbps = controller.Options.AudioNormalKbps;
    }

    public MediaSendPacer Pacer { get; }
    public SendRateController Controller { get; }
    /// <summary>The camera's ladder. Replaced when the camera is (re)started with a new preference.</summary>
    public VideoRateLadder Ladder { get; set; }
    public SendPathSignals Signals => _signals;
    public bool VideoSuspended => _suspended;

    /// <param name="nowMs">Monotonic time.</param>
    /// <param name="encodeBacklog">Frames queued in front of the video encoder (the CPU/thermal signal).</param>
    public SendRateTick Tick(long nowMs, int encodeBacklog = 0)
    {
        var pacer = Pacer.Sample();
        var sentVideo = Math.Max(0, pacer.SentKbps - pacer.AudioKbps);
        var (relay, receiver) = _signals.Take(nowMs, sentVideo, pacer.AudioKbps, Controller.Options.ReportFreshMs);
        var decision = Controller.Update(new SendPathSample(nowMs, pacer.SentKbps, pacer.AudioKbps, pacer.QueueDelayMs,
            pacer.CapacityKbps, pacer.BaseLosses > 0, relay, receiver));
        Pacer.RateKbps = decision.TotalKbps;

        // A phone that cannot encode fast enough is capped on its own, whatever the network says.
        var cpuChanged = false;
        if (encodeBacklog >= 2)
        {
            _cpuCalm = 0;
            if (++_cpuStrained >= CpuStrainedAfter) { _cpuStrained = 0; cpuChanged = Ladder.ReduceForCpu(); }
        }
        else
        {
            _cpuStrained = 0;
            if (++_cpuCalm >= CpuRelaxAfter) { _cpuCalm = 0; Ladder.RelaxCpuCeiling(); }
        }

        var suspend = decision.VideoSuspended && !_suspended;
        var resume = !decision.VideoSuspended && _suspended;
        _suspended = decision.VideoSuspended;
        VideoSetting? video = null;
        if (!_suspended)
        {
            video = Ladder.Place(decision.VideoKbps, nowMs, congested: decision.Signal != RateSignal.Normal);
            if ((resume || cpuChanged) && video is null) video = Ladder.Current;
        }

        int? audio = decision.AudioKbps != _audioKbps ? decision.AudioKbps : null;
        _audioKbps = decision.AudioKbps;
        return new SendRateTick(decision, video, suspend, resume, audio, pacer, relay, receiver);
    }
}
