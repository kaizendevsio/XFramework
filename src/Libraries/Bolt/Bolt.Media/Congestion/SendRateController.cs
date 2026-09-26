namespace Bolt.Media.Congestion;

/// <summary>Tuning for <see cref="SendRateController"/>. Defaults target a WebSocket (TCP) call path.</summary>
public sealed class SendRateOptions
{
    /// <summary>Never estimate below this: voice alone needs about this much on the wire.</summary>
    public int MinTotalKbps { get; init; } = 48;
    public int MaxTotalKbps { get; init; } = 40_000;
    /// <summary>Queuing delay the controller keeps the path below.</summary>
    public int TargetDelayMs { get; init; } = 150;
    /// <summary>Above this the path is overused whatever the trend.</summary>
    public int HighDelayMs { get; init; } = 400;
    /// <summary>Above the target, delay growing faster than this (ms per second) is overuse.</summary>
    public int OveruseGradientMsPerSecond { get; init; } = 80;
    /// <summary>A decrease sets the estimate to this fraction of the measured capacity, leaving room to drain.</summary>
    public double DecreaseFactor { get; init; } = 0.85;
    /// <summary>After a decrease the queue needs time to drain before the next decision is meaningful.</summary>
    public int HoldAfterDecreaseMs { get; init; } = 700;
    /// <summary>Calm time required before any increase.</summary>
    public int IncreaseAfterMs { get; init; } = 1_500;
    /// <summary>Growth per second before the path has ever shown congestion.</summary>
    public double StartupGainPerSecond { get; init; } = 0.15;
    /// <summary>Growth per second far from the last congestion point.</summary>
    public double ProbeGainPerSecond { get; init; } = 0.08;
    /// <summary>Growth per second close to the last congestion point.</summary>
    public double NearGainPerSecond { get; init; } = 0.025;
    /// <summary>Remote reports older than this are ignored.</summary>
    public int ReportFreshMs { get; init; } = 1_500;
    /// <summary>Without any remote report for this long after the start, the controller trusts local signals alone.</summary>
    public int RemoteGraceMs { get; init; } = 5_000;

    /// <summary>Video is suspended when its budget stays below this for <see cref="SuspendAfterMs"/>.</summary>
    public int SuspendVideoKbps { get; init; } = 60;
    public int SuspendAfterMs { get; init; } = 3_000;
    /// <summary>Video resumes once its budget reaches this and has been calm for the (backed-off) resume hold.</summary>
    public int ResumeVideoKbps { get; init; } = 110;
    public int ResumeHoldMs { get; init; } = 3_000;
    public int MaxResumeHoldMs { get; init; } = 48_000;
    /// <summary>A suspension this soon after resuming counts as a failed resume and doubles the hold.</summary>
    public int FailedResumeWindowMs { get; init; } = 10_000;

    /// <summary>Opus rate under congestion, normally, and with clear headroom.</summary>
    public int AudioLowKbps { get; init; } = 24;
    public int AudioNormalKbps { get; init; } = 32;
    public int AudioHighKbps { get; init; } = 40;
    public int AudioLowBelowKbps { get; init; } = 260;
    public int AudioHighAboveKbps { get; init; } = 2_500;
    public int AudioHighAfterCalmMs { get; init; } = 20_000;
    /// <summary>
    /// After an outage (the transport was replaced, or every receiver went quiet for <see cref="ReturnAfterSilenceMs"/>
    /// and came back calm) the estimate restarts from <see cref="RestartFraction"/> of the pre-outage stable rate,
    /// never below this floor. 0 means the resume point of video (<see cref="ResumeVideoKbps"/> plus audio). The new
    /// path is untested (a resume may be on another network), so neither the old peak nor the cut the outage caused
    /// is a good start.
    /// </summary>
    public int RestartFloorKbps { get; init; }
    public double RestartFraction { get; init; } = 0.5;
    /// <summary>Receiver silence at least this long, ended by a calm report, is an outage the path came back from.</summary>
    public int ReturnAfterSilenceMs { get; init; } = 5_000;
    /// <summary>Time constant of the stable rate: the calm operating point, not a probe's peak.</summary>
    public int StableTimeConstantMs { get; init; } = 10_000;
    /// <summary>Bolt, SFrame and WS/TLS/TCP bytes on every audio packet, and packets per second.</summary>
    public int AudioOverheadBytes { get; init; } = 130;
    public int AudioPacketsPerSecond { get; init; } = 50;
}

/// <summary>Latest relay congestion report (see Bolt.Protocol MediaCongestionData), in controller terms.</summary>
/// <param name="ReceivedAtMs">When the sender received it.</param>
/// <param name="QueueDelayMs">Oldest media queued towards the worst receiver.</param>
/// <param name="UplinkDelayMs">Sender-to-relay queuing.</param>
/// <param name="CapacityKbps">What the worst backlogged receiver can take of this sender's media, audio and video together; 0 = no receiver is limited.</param>
/// <param name="Dropping">The relay shed this sender's pictures since its previous report (enhancement layers count).</param>
/// <param name="BaseLost">A receiver lost the base layer and waits for a keyframe: its queue overflowed.</param>
public readonly record struct RelaySignal(long ReceivedAtMs, int QueueDelayMs, int UplinkDelayMs, int CapacityKbps, bool Dropping,
    bool BaseLost = false);

/// <summary>Latest receiver delay reports, aggregated to the worst receiver.</summary>
/// <param name="ReceivedAtMs">When the sender received the most recent one.</param>
/// <param name="QueueDelayMs">One-way delay above its recent minimum, end to end.</param>
/// <param name="ReceivedKbps">What that receiver actually got of this sender's media, audio and video together.</param>
public readonly record struct ReceiverSignal(long ReceivedAtMs, int QueueDelayMs, int ReceivedKbps);

/// <summary>One observation window of the send path.</summary>
/// <param name="NowMs">Monotonic time.</param>
/// <param name="SentKbps">Media actually handed to the transport in the window (audio and video).</param>
/// <param name="AudioKbps">The audio part of <paramref name="SentKbps"/>, including packet overhead.</param>
/// <param name="LocalQueueDelayMs">Age of what waits in the sender's own queues, plus the transport backlog at the delivery rate.</param>
/// <param name="LocalCapacityKbps">Uplink rate measured while the pacer was blocked on the transport; 0 = not link limited.</param>
/// <param name="LocalBaseLost">The sender's own queue lost a base-layer picture in the window (and now waits for a keyframe).</param>
public readonly record struct SendPathSample(
    long NowMs,
    int SentKbps,
    int AudioKbps,
    int LocalQueueDelayMs,
    int LocalCapacityKbps,
    bool LocalBaseLost,
    RelaySignal? Relay = null,
    ReceiverSignal? Receiver = null);

public enum RateSignal { Normal, Overuse, Hold }

/// <summary>What the call may send now.</summary>
/// <param name="TotalKbps">The estimate: everything this sender may put on the wire.</param>
/// <param name="AudioKbps">Opus encoder rate.</param>
/// <param name="VideoKbps">Video budget (0 while suspended).</param>
/// <param name="VideoSuspended">Video stands down so voice keeps the link.</param>
/// <param name="Signal">What the last window was judged to be.</param>
/// <param name="DelayMs">The queuing delay the decision was based on.</param>
public readonly record struct SendRateDecision(int TotalKbps, int AudioKbps, int VideoKbps, bool VideoSuspended, RateSignal Signal, int DelayMs);

/// <summary>
/// Delay-based send-rate control for a call on a reliable (TCP) path, GCC/BBR-flavoured and deliberately simple.
///
/// Signals: the sender's own queues (pacer + transport backlog), the relay's report (the worst receiver's queue,
/// the uplink's queuing delay, the capacity that receiver drained at, and drops), and receivers' end-to-end delay
/// reports. The largest delay wins. Over TCP nothing is lost, so the congestion signal is delay and drops; loss
/// never appears. What does appear is the TCP stall: one lost segment holds the whole connection for about a
/// round trip, so the delay spikes and then drains in a burst even when the rate fits. A controller that cut on
/// every spike would sit far below what a lossy high-RTT link carries, so a queue has to persist or keep growing
/// to count.
///
/// - <b>Fast down.</b> Overuse is a lost base-layer picture (a queue overflowed), a delay floor (the smallest delay
///   of the last second) above <see cref="SendRateOptions.TargetDelayMs"/>, or a delay above
///   <see cref="SendRateOptions.HighDelayMs"/> that is still growing after three reports. The estimate then falls to
///   <see cref="SendRateOptions.DecreaseFactor"/> times the best capacity measurement (never more than that factor of
///   itself): what the relay drained a backlogged receiver at, what the sender's own transport drained, or what a
///   receiver actually got; without any, the sent rate divided by how fast the queue grows. A 1080p offer on a
///   512 kbps link lands on the link within about a second, then holds while the queue drains.
/// - <b>Slow up.</b> Only after <see cref="SendRateOptions.IncreaseAfterMs"/> of calm, only with fresh remote reports
///   (no news is not good news on a dead link), faster before the first congestion, cautiously near the last
///   congestion point, and never far above what is actually being sent.
/// - <b>Hysteresis.</b> Between "calm" and "overuse" the estimate holds. Video suspension needs a sustained low
///   budget, and a resume that fails doubles the wait before the next one.
/// </summary>
public sealed class SendRateController
{
    /// <summary>After a decrease, how long a queue that is not growing further counts as draining.</summary>
    private const int DrainWindowMs = 4_000;
    /// <summary>Reports in a row a high, rising delay must persist before it counts on its own: a stall drains, overload grows.</summary>
    private const int RisingSamples = 3;
    /// <summary>
    /// The delay floor is the smallest delay over this window: a queue that never drained below target in it is
    /// standing. Two seconds outlasts the stall a lost segment causes on a 500 ms round trip (the queue drains in a
    /// burst as soon as the retransmission lands); growing queues are caught sooner by the slope tests.
    /// </summary>
    private const int FloorWindowMs = 2_000;
    /// <summary>
    /// A queue growing faster than this (ms per second) is overload on its own: a stall grows the delay at most one
    /// second per second (nothing is delivered), so faster growth means more than twice the link is being offered.
    /// </summary>
    private const int OverloadGradientMsPerSecond = 1_500;
    /// <summary>
    /// The same test over the last half second (and, at 1.6 s per second, the last quarter), for a sudden step the
    /// one-second slope still averages away.
    /// </summary>
    private const int OverloadJumpMs = 750;
    /// <summary>A receiver getting less than this share of what is sent is on a link that shrank.</summary>
    private const double StarvedFraction = 0.4;
    /// <summary>How long a receiver that stopped reporting still counts (see <see cref="SendPathSignals.SilentReceiverMs"/>).</summary>
    private const int SilentReceiverMs = SendPathSignals.SilentReceiverMs;

    private readonly SendRateOptions _options;
    private readonly Queue<(long At, int Delay)> _history = new();
    private double _estimate;
    private double _lastCongestionKbps;
    private bool _congestedOnce;
    private long _startedAt = long.MinValue;
    private long _lastUpdateAt = long.MinValue;
    private long _lastDecreaseAt = long.MinValue / 2;
    private int _delayAtDecrease;
    private int _risingStreak;
    /// <summary>Cuts since the path was last calm. The first one is mild (a stall looks like congestion); the next ones are not.</summary>
    private int _cutStreak;
    private long? _calmSince;
    private long? _lowVideoSince;
    private bool _suspended;
    private long _suspendedAt = long.MinValue / 2, _resumedAt = long.MinValue / 2;
    private int _resumeHoldMs;
    private bool _remoteSeen;
    private int _audioKbps;
    /// <summary>The calm operating point (see <see cref="StableKbps"/>), and the estimate at the last calm tick.</summary>
    private double _stable, _lastCalmEstimate;
    private long? _lastFreshReceiverAt;

    public SendRateController(int initialTotalKbps, SendRateOptions? options = null)
    {
        _options = options ?? new SendRateOptions();
        _estimate = Math.Clamp(initialTotalKbps, _options.MinTotalKbps, _options.MaxTotalKbps);
        _resumeHoldMs = _options.ResumeHoldMs;
        _audioKbps = _options.AudioNormalKbps;
        _stable = _lastCalmEstimate = _estimate;
    }

    /// <summary>
    /// The rate this path was last calm at: a slow average of the estimate over calm windows only, capped by the
    /// estimate of the last calm window. Congested windows and the cuts an outage causes never lower it directly, and
    /// a probe's short-lived peak barely raises it; a path that settles lower after congestion pulls it down.
    /// </summary>
    /// <summary>How often the path was started over after an outage (see <see cref="SendRateOptions.ReturnAfterSilenceMs"/>); the loop restarts the ladder with it.</summary>
    public int Restarts { get; private set; }

    public int StableKbps => (int)Math.Round(Math.Min(_stable, _lastCalmEstimate));

    /// <summary>Where a new or returning path starts: <see cref="SendRateOptions.RestartFraction"/> of the stable rate, above the floor.</summary>
    public static int RestartKbps(int stableKbps, SendRateOptions options, int audioWireKbps)
    {
        var floor = options.RestartFloorKbps > 0 ? options.RestartFloorKbps : options.ResumeVideoKbps + audioWireKbps + 16;
        return Math.Clamp((int)Math.Round(Math.Max(floor, stableKbps * options.RestartFraction)), options.MinTotalKbps, options.MaxTotalKbps);
    }

    /// <summary>
    /// A controller for a transport that replaced one lost to an outage (a resumed call): it starts from
    /// <see cref="RestartKbps"/> of the old controller's stable rate, with none of the old path's delay history,
    /// congestion point or suspension. The caller also brings a new pacer, so queue and capacity state start empty.
    /// </summary>
    public static SendRateController Resume(SendRateController previous, int audioWireKbps)
    {
        var options = previous._options;
        return new SendRateController(RestartKbps(previous.StableKbps, options, audioWireKbps), options);
    }

    public SendRateOptions Options => _options;
    public int EstimateKbps => (int)Math.Round(_estimate);
    public bool VideoSuspended => _suspended;
    /// <summary>Current wait between a suspension and a resume attempt.</summary>
    public int ResumeHoldMs => _resumeHoldMs;

    /// <summary>
    /// Start over from a new offer, for example when the camera is turned on: the path's history stays, only the
    /// estimate moves (and never above what was last found to congest it).
    /// </summary>
    public void Reset(int totalKbps)
    {
        var ceiling = _lastCongestionKbps > 0 ? _lastCongestionKbps : _options.MaxTotalKbps;
        _estimate = Math.Clamp(Math.Min(totalKbps, ceiling), _options.MinTotalKbps, _options.MaxTotalKbps);
        _suspended = false;
        _lowVideoSince = null;
    }

    public SendRateDecision Update(in SendPathSample sample)
    {
        var now = sample.NowMs;
        if (_startedAt == long.MinValue) _startedAt = now;
        var dt = _lastUpdateAt == long.MinValue ? 0 : Math.Clamp((now - _lastUpdateAt) / 1000.0, 0, 1);
        _lastUpdateAt = now;

        _sentAverage = _sentAverage <= 0 ? sample.SentKbps : _sentAverage * 0.75 + sample.SentKbps * 0.25;
        var fresh = _options.ReportFreshMs;
        var relay = sample.Relay is { } r && now - r.ReceivedAtMs <= fresh ? r : (RelaySignal?)null;
        var receiver = sample.Receiver is { } q && now - q.ReceivedAtMs <= fresh ? q : (ReceiverSignal?)null;
        // A receiver that was reporting and went quiet: its reports ride back over the congested path (the ACKs for
        // them queue behind our own media), so silence during a queue means the queue is still there and growing.
        var silent = receiver is null && sample.Receiver is { } quiet && now - quiet.ReceivedAtMs <= SilentReceiverMs ? quiet : (ReceiverSignal?)null;
        if (relay is not null || receiver is not null) _remoteSeen = true;
        if (receiver is { } back)
        {
            // Every receiver went quiet for a long time and reports again, calmly: that was an outage (a receiver
            // resuming on a new connection, maybe a new network), not a queue. Start that path over.
            if (_lastFreshReceiverAt is { } lastHeard && back.ReceivedAtMs - lastHeard >= _options.ReturnAfterSilenceMs &&
                back.QueueDelayMs < _options.TargetDelayMs)
                RestartAfterOutage(now, AudioWireKbps(sample));
            _lastFreshReceiverAt = Math.Max(_lastFreshReceiverAt ?? back.ReceivedAtMs, back.ReceivedAtMs);
        }
        // Increases need someone downstream saying the path is fine, unless nobody downstream ever reports.
        var informed = silent is null &&
                       (relay is not null || receiver is not null || (!_remoteSeen && now - _startedAt >= _options.RemoteGraceMs));

        var delay = sample.LocalQueueDelayMs;
        if (relay is { } rs) delay = Math.Max(delay, rs.QueueDelayMs + rs.UplinkDelayMs);
        if (receiver is { } rq) delay = Math.Max(delay, rq.QueueDelayMs);
        else if (silent is { } sq && sq.QueueDelayMs >= _options.TargetDelayMs)
            delay = Math.Max(delay, sq.QueueDelayMs + (int)Math.Min(int.MaxValue / 2, now - sq.ReceivedAtMs));
        var gradient = Gradient(now, delay);
        var floor = Floor(now);

        var baseLost = sample.LocalBaseLost || relay?.BaseLost == true;
        var rising = delay >= _options.HighDelayMs && gradient >= _options.OveruseGradientMsPerSecond;
        _risingStreak = rising ? _risingStreak + 1 : 0;
        var standing = floor >= _options.TargetDelayMs;
        // Shed enhancement layers are the relay protecting a receiver; with a queue that also stands, it is congestion.
        var shedding = relay?.Dropping == true && floor >= _options.TargetDelayMs / 2;
        // A queue growing faster than any stall could make it, or a receiver getting a fraction of what is sent
        // while its delay rises: the link shrank (a handover, a step down). Either is overload on its own.
        var jump = _history.Count >= 3 &&
                   (delay - DelayAgo(now, 500) >= OverloadJumpMs || delay - DelayAgo(now, 250) >= OverloadJumpMs * 8 / 15);
        var starved = receiver is { ReceivedKbps: > 0 } got && delay >= _options.TargetDelayMs && _sentAverage > 0 &&
                      got.ReceivedKbps < _sentAverage * StarvedFraction;
        var overload = delay >= _options.HighDelayMs && (gradient >= OverloadGradientMsPerSecond || jump || starved);
        var overuse = baseLost || standing || shedding || overload || _risingStreak >= RisingSamples;
        // Probing needs the path quiet now as well: a spike (a stall, a keyframe) holds the estimate for that tick.
        var calm = !overuse && informed && floor < _options.TargetDelayMs / 2 && delay < _options.TargetDelayMs;
        // After a decrease the queue keeps growing for a feedback delay and then drains: drops and a high delay in
        // that time are the old rate, not new congestion, and cutting again is how controllers undershoot. With a
        // capacity measurement the test is exact (the estimate already fits the link); without one, the delay must
        // not have grown much past where it was when the cut was made.
        var capacityNow = Capacity(sample, relay, receiver ?? silent, delay);
        var draining = overuse && now - _lastDecreaseAt < DrainWindowMs && !overloadAfterCut(capacityNow) &&
                       (capacityNow > 0 ? _estimate <= capacityNow * 0.95 : delay <= _delayAtDecrease + Math.Max(150, _delayAtDecrease / 2));
        bool overloadAfterCut(double capacity) => capacity > 0 && _estimate > capacity * 1.2;

        RateSignal signal;
        if (draining)
        {
            signal = RateSignal.Hold;
            _calmSince = null;
        }
        else if (overuse)
        {
            signal = RateSignal.Overuse;
            _calmSince = null;
            // Once decreased, wait for the queue to drain unless it keeps getting much worse.
            var deeper = delay >= _options.HighDelayMs * 2 && gradient > 0;
            if (now - _lastDecreaseAt >= _options.HoldAfterDecreaseMs || (deeper && now - _lastDecreaseAt >= _options.HoldAfterDecreaseMs / 2))
                Decrease(now, sample, relay, receiver ?? silent, delay, severe: baseLost || overload || _cutStreak > 0);
        }
        else if (calm)
        {
            signal = RateSignal.Normal;
            _calmSince ??= now;
            _lastCalmEstimate = _estimate;
            if (dt > 0) _stable += (_estimate - _stable) * Math.Min(1, dt * 1000 / Math.Max(1, _options.StableTimeConstantMs));
            if (now - _calmSince.Value >= _options.IncreaseAfterMs) _cutStreak = 0;
            if (informed && now - _calmSince.Value >= _options.IncreaseAfterMs && now - _lastDecreaseAt >= _options.IncreaseAfterMs)
                Increase(dt, sample);
        }
        else
        {
            signal = RateSignal.Hold;
            _calmSince = null;
        }

        _estimate = Math.Clamp(_estimate, _options.MinTotalKbps, _options.MaxTotalKbps);
        return Allocate(now, sample, signal, delay);
    }

    private void RestartAfterOutage(long now, int audioWireKbps)
    {
        _estimate = RestartKbps(StableKbps, _options, audioWireKbps);
        Restarts++;
        _history.Clear();
        _lastCongestionKbps = 0;
        _congestedOnce = false;
        _lastDecreaseAt = long.MinValue / 2;
        _delayAtDecrease = 0;
        _risingStreak = _cutStreak = 0;
        _calmSince = null;
        _lowVideoSince = null;
        _resumeHoldMs = _options.ResumeHoldMs;
        // Video comes back with the path when the restart affords it; otherwise the usual resume rule applies.
        if (_suspended && _estimate - audioWireKbps >= _options.ResumeVideoKbps)
        {
            _suspended = false;
            _resumedAt = now;
        }
    }

    /// <summary>
    /// The link's capacity as measured on a backlogged path, or 0. Downstream, the relay's drain rate and what the
    /// receiver actually got describe the same link, and each can be wrong: the relay only sees how fast its socket
    /// takes data, which a deep buffer further on (a proxy, a radio, a big TCP window) makes look far faster than
    /// the link, and a receiver's first reports average over a window that was not yet busy. When they roughly
    /// agree the larger wins; when the relay claims far more, the receiver's end-to-end view wins. The sender's own
    /// uplink is a different link: the smaller of the two ends is the path.
    /// </summary>
    private double Capacity(in SendPathSample sample, RelaySignal? relay, ReceiverSignal? receiver, int delay)
    {
        var downstream = 0.0;
        if (relay is { CapacityKbps: > 0 } rs) downstream = rs.CapacityKbps;
        // What a receiver got while its queue grew is what the path carries.
        if (receiver is { ReceivedKbps: > 0 } rq && delay >= _options.TargetDelayMs)
            downstream = downstream > 0 && downstream <= rq.ReceivedKbps * 2 ? Math.Max(downstream, rq.ReceivedKbps) : rq.ReceivedKbps;
        if (sample.LocalCapacityKbps > 0)
            return downstream > 0 ? Math.Min(downstream, sample.LocalCapacityKbps) : sample.LocalCapacityKbps;
        return downstream;
    }

    /// <param name="severe">A queue overflowed, is growing faster than a stall could make it, or is still there after a
    /// first cut: cut to the link. Otherwise (the first sign of a standing or growing queue, which a single TCP stall
    /// also produces) at most a quarter goes, and a path that turns calm again loses nothing more.</param>
    private void Decrease(long now, in SendPathSample sample, RelaySignal? relay, ReceiverSignal? receiver, int delay, bool severe)
    {
        var capacity = Capacity(sample, relay, receiver, delay);
        var measured = capacity > 0;
        // No measurement yet (the first reports of an overload): a queue growing by g ms every second means the
        // link takes 1000 / (1000 + g) of what is sent.
        if (!measured) capacity = sample.SentKbps > 0 ? sample.SentKbps * 1000.0 / (1000 + Math.Max(0, _lastGradient)) : _estimate;
        var basis = Math.Min(_estimate, capacity);
        // A deep queue needs a deeper cut to drain within a few seconds.
        // A deep queue needs a deeper cut to drain within a few seconds; a standing queue of seconds (a buffer that
        // filled when the link shrank) is best drained almost without video.
        var factor = !severe ? _options.DecreaseFactor
            : Floor(now) >= _options.HighDelayMs * 4 ? 0.35
            : delay >= _options.HighDelayMs * 2 ? _options.DecreaseFactor - 0.25
            : _options.DecreaseFactor;
        // The congestion point is the link's measured capacity where there is one: probing slows near it.
        _lastCongestionKbps = measured ? capacity : basis;
        _congestedOnce = true;
        var next = basis * factor;
        if (!severe)
        {
            // A first sign is not proof: it never costs more than a quarter, and never the picture itself.
            next = Math.Max(next, _estimate * 0.75);
            next = Math.Max(next, Math.Min(_estimate, AudioWireKbps(sample) + _options.SuspendVideoKbps + 20));
        }
        _estimate = Math.Max(_options.MinTotalKbps, Math.Min(_estimate, next));
        _lastDecreaseAt = now;
        _delayAtDecrease = delay;
        _cutStreak++;
    }

    private void Increase(double dt, in SendPathSample sample)
    {
        if (dt <= 0) return;
        double gain;
        // Before any congestion, and far below the last congestion point (after draining a flooded buffer), climb fast.
        if (!_congestedOnce || (_lastCongestionKbps > 0 && _estimate < _lastCongestionKbps * 0.5)) gain = _options.StartupGainPerSecond;
        else if (_lastCongestionKbps > 0 && _estimate >= _lastCongestionKbps * 0.9 && _estimate <= _lastCongestionKbps * 1.2)
            gain = _options.NearGainPerSecond;
        else gain = _options.ProbeGainPerSecond;
        var next = _estimate * (1 + gain * dt);
        // An encoder that cannot use its budget must not let the estimate run away from the link it has never
        // tested. While video is suspended there is nothing to test with, so the ceiling is the resume point.
        var floorForResume = ResumeTotalKbps(sample) + 16;
        var ceiling = _suspended ? floorForResume : Math.Max(sample.SentKbps * 1.5 + 64, floorForResume);
        if (next > ceiling) next = Math.Max(_estimate, ceiling);
        _estimate = next;
        if (_lastCongestionKbps > 0 && _estimate > _lastCongestionKbps * 1.3) _lastCongestionKbps = 0;
    }

    private int AudioWireKbps(in SendPathSample sample) =>
        sample.AudioKbps > 0 ? sample.AudioKbps : _audioKbps + _options.AudioOverheadBytes * 8 * _options.AudioPacketsPerSecond / 1000;

    private int ResumeTotalKbps(in SendPathSample sample) => _options.ResumeVideoKbps + AudioWireKbps(sample);

    private SendRateDecision Allocate(long now, in SendPathSample sample, RateSignal signal, int delay)
    {
        var total = (int)Math.Round(_estimate);
        // Opus stays at a robust rate; it drops only when the link is tiny and climbs only with clear headroom.
        if (total < _options.AudioLowBelowKbps) _audioKbps = _options.AudioLowKbps;
        else if (total >= _options.AudioHighAboveKbps && _calmSince is { } since && now - since >= _options.AudioHighAfterCalmMs)
            _audioKbps = _options.AudioHighKbps;
        else if (total >= _options.AudioLowBelowKbps * 5 / 4 && total < _options.AudioHighAboveKbps * 4 / 5)
            _audioKbps = _options.AudioNormalKbps;

        var video = total - AudioWireKbps(sample);
        if (_suspended)
        {
            var calmFor = _calmSince is { } calmStart ? now - calmStart : 0;
            if (video >= _options.ResumeVideoKbps && calmFor >= _resumeHoldMs && now - _suspendedAt >= _resumeHoldMs)
            {
                _suspended = false;
                _resumedAt = now;
                _lowVideoSince = null;
            }
        }
        else if (video < _options.SuspendVideoKbps)
        {
            _lowVideoSince ??= now;
            if (now - _lowVideoSince.Value >= _options.SuspendAfterMs)
            {
                _suspended = true;
                // A resume that did not hold means the link is still bad: wait longer next time.
                _resumeHoldMs = now - _resumedAt <= _options.FailedResumeWindowMs
                    ? Math.Min(_resumeHoldMs * 2, _options.MaxResumeHoldMs)
                    : _options.ResumeHoldMs;
                _suspendedAt = now;
            }
        }
        else
        {
            _lowVideoSince = null;
        }

        return new SendRateDecision(total, _audioKbps, _suspended ? 0 : Math.Max(0, video), _suspended, signal, delay);
    }

    private double _lastGradient;
    private double _sentAverage;

    /// <summary>The delay about <paramref name="agoMs"/> ago (the oldest sample if the history is shorter).</summary>
    private int DelayAgo(long now, int agoMs)
    {
        var value = -1;
        foreach (var (at, delay) in _history)
        {
            if (now - at < agoMs) break;
            value = delay;
        }
        return value < 0 ? _history.Peek().Delay : value;
    }

    /// <summary>The smallest delay of the last <see cref="FloorWindowMs"/> (the current one until the window has history).</summary>
    private int Floor(long now)
    {
        var floor = int.MaxValue;
        foreach (var (at, value) in _history)
            if (now - at <= FloorWindowMs) floor = Math.Min(floor, value);
        return floor == int.MaxValue ? 0 : floor;
    }

    /// <summary>Least-squares slope of the delay over the last second, in ms per second.</summary>
    private double Gradient(long now, int delay)
    {
        _history.Enqueue((now, delay));
        while (_history.Count > 0 && now - _history.Peek().At > FloorWindowMs) _history.Dequeue();
        _lastGradient = GradientOfHistory(now);
        return _lastGradient;
    }

    private double GradientOfHistory(long now)
    {
        double n = 0, sx = 0, sy = 0, sxx = 0, sxy = 0;
        foreach (var (at, value) in _history)
        {
            if (now - at > 1_000) continue;
            n++;
            var x = (at - now) / 1000.0;
            sx += x; sy += value; sxx += x * x; sxy += x * value;
        }
        var denominator = n * sxx - sx * sx;
        return n < 3 || denominator <= 1e-9 ? 0 : (n * sxy - sx * sy) / denominator;
    }
}
