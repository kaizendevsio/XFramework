namespace Bolt.Media.Congestion;

/// <summary>What one step of a start probe offered, and what each direction of the link carried of it.</summary>
/// <param name="OfferedKbps">The rate the step was paced at.</param>
/// <param name="Sent">Probe messages handed to the channel in the step.</param>
/// <param name="UplinkKbps">What reached the relay, by its arrival times (0: no measurement).</param>
/// <param name="UplinkLost">Messages the relay reported missing.</param>
/// <param name="UplinkDelayGrowthMs">How much later the step's last messages arrived than its first, beyond their spacing: a queue building on the uplink.</param>
/// <param name="EchoKbps">What came back, by local arrival times (0: no echo).</param>
/// <param name="Echoed">Echoes that came back.</param>
/// <param name="EchoDelayGrowthMs">The same queue test on the round trip.</param>
/// <param name="UplinkPassed">The uplink carried the step: the rate, without loss or a queue.</param>
/// <param name="EchoPassed">The round trip carried it too.</param>
/// <param name="EchoRequested">The step asked the relay for its echo (only while every earlier round trip passed).</param>
/// <param name="SentKbps">What the device actually handed to its channel over the step (a browser holding back its own
/// buffer sends less than the pace); 0 when too few messages to tell.</param>
public readonly record struct ProbeStep(int OfferedKbps, int Sent, int UplinkKbps, int UplinkLost, int UplinkDelayGrowthMs,
    int EchoKbps, int Echoed, int EchoDelayGrowthMs, bool UplinkPassed, bool EchoPassed, bool EchoRequested = true, int SentKbps = 0)
{
    /// <summary>The rate the step really tested: its pace, or less when the device could not send that much.</summary>
    public int TestedKbps => SentKbps > 0 ? Math.Min(OfferedKbps, SentKbps) : OfferedKbps;

    public override string ToString() =>
        $"{OfferedKbps}k{(TestedKbps < OfferedKbps ? $" (sent {SentKbps}k)" : "")}: up {UplinkKbps}k{(UplinkPassed ? "" : "!")} (lost {UplinkLost}/{Sent}, +{UplinkDelayGrowthMs}ms)" +
        (EchoRequested ? $" echo {EchoKbps}k{(EchoPassed ? "" : "!")} ({Echoed}/{Sent}, +{EchoDelayGrowthMs}ms)" : "");
}

/// <summary>
/// What a start probe found. <see cref="UplinkKbps"/> is what this device can send to the relay; <see cref="DownlinkKbps"/>
/// what the relay can send to it (null when the relay does not echo). An "at least" value is a lower bound: the link carried
/// everything the probe offered (or, for the downlink, everything the uplink let through), so its limit lies above.
/// </summary>
public sealed record LinkProbeResult(int UplinkKbps, bool UplinkAtLeast, int? DownlinkKbps, bool DownlinkAtLeast, int? RttMs,
    IReadOnlyList<ProbeStep> Steps, long DurationMs)
{
    public override string ToString() =>
        $"up {(UplinkAtLeast ? ">=" : "")}{UplinkKbps}k down {(DownlinkKbps is { } d ? (DownlinkAtLeast ? ">=" : "") + d + "k" : "-")} " +
        $"rtt {(RttMs is { } r ? r + "ms" : "-")} in {DurationMs}ms [{string.Join("; ", Steps)}]";
}

/// <summary>
/// The measurement half of a call's start probe (WebRTC-style initial probing): a few short paced bursts of padding at
/// rising rates through the relay, before media needs the link. The relay's transport feedback times each message's
/// arrival on the uplink; the relay's echo times the round trip, so the downlink too. A step passes when the link
/// carried its rate without a queue building or loss; the probe stops at the first step that does not, and that step's
/// delivered rate is the link's.
///
/// The driver (<see cref="MediaTransportClient.ProbeAsync"/>) paces and sends; this class only records and judges, so
/// the judgement is testable without a network.
/// </summary>
public sealed class LinkProbe
{
    /// <summary>Rates offered, lowest first. The top one is above what a 1440p30 picture needs, so passing it leaves no doubt.</summary>
    public static readonly int[] DefaultStepsKbps = [600, 1_500, 3_500, 7_500];
    public const int DefaultStepMs = 200;
    /// <summary>A queue that grew this much over one step is the link saying no.</summary>
    public const int MaxDelayGrowthMs = 25;
    /// <summary>
    /// The same for the round trip, which also carries the time a busy page takes to handle each echo (WebKit delivers a
    /// data channel's messages to .NET in bursts): only a queue well beyond that counts.
    /// </summary>
    public const int MaxEchoGrowthMs = 60;
    /// <summary>A step must deliver at least this share of its rate.</summary>
    public const double MinDeliveredShare = 0.85;
    /// <summary>Random loss on a mobile link stays under this; a full shallow queue does not.</summary>
    public const double MaxLossShare = 0.1;

    private readonly object _sync = new();
    private readonly Dictionary<ushort, (int Step, int Bytes, long SentUs)> _byTransport = [];
    private readonly Dictionary<(int Step, uint Index), (int Bytes, long SentUs)> _byIndex = [];
    private readonly List<StepLog> _steps = [];

    private sealed class StepLog(int offeredKbps)
    {
        public int OfferedKbps { get; } = offeredKbps;
        public int Sent;
        public long SentBytes, FirstSentUs = long.MaxValue, LastSentUs;
        public readonly List<(long SentUs, long ArrivalUs, int Bytes)> Arrivals = [];
        public int Lost;
        public readonly List<(long SentUs, long ArrivalUs, int Bytes)> Echoes = [];
    }

    /// <summary>Begin a step paced at <paramref name="offeredKbps"/>; returns its number.</summary>
    public int BeginStep(int offeredKbps)
    {
        lock (_sync)
        {
            _steps.Add(new StepLog(offeredKbps));
            return _steps.Count - 1;
        }
    }

    /// <summary>One probe message left: its transport sequence (null when the path does not stamp), its index within its step, and its size on the wire.</summary>
    public void OnSent(int step, uint index, ushort? transportSequence, int bytes, long sentUs)
    {
        lock (_sync)
        {
            if (step < 0 || step >= _steps.Count) return;
            var log = _steps[step];
            log.Sent++;
            log.SentBytes += bytes;
            log.FirstSentUs = Math.Min(log.FirstSentUs, sentUs);
            log.LastSentUs = Math.Max(log.LastSentUs, sentUs);
            _byIndex[(step, index)] = (bytes, sentUs);
            if (transportSequence is { } sequence) _byTransport[sequence] = (step, bytes, sentUs);
        }
    }

    /// <summary>A transport feedback report: relay arrival times (µs, its clock) from <paramref name="baseSequence"/> on, negative for lost.</summary>
    public void OnFeedback(ushort baseSequence, IReadOnlyList<long> arrivalUs)
    {
        lock (_sync)
        {
            for (var offset = 0; offset < arrivalUs.Count; offset++)
            {
                var sequence = unchecked((ushort)(baseSequence + offset));
                if (!_byTransport.Remove(sequence, out var sent)) continue;
                var log = _steps[sent.Step];
                if (arrivalUs[offset] < 0) log.Lost++;
                else log.Arrivals.Add((sent.SentUs, arrivalUs[offset], sent.Bytes));
            }
        }
    }

    /// <summary>The relay sent a probe message back.</summary>
    public void OnEcho(int step, uint index, long arrivalUs)
    {
        lock (_sync)
        {
            if (!_byIndex.Remove((step, index), out var sent) || step < 0 || step >= _steps.Count) return;
            _steps[step].Echoes.Add((sent.SentUs, arrivalUs, sent.Bytes));
        }
    }

    /// <summary>Messages of <paramref name="step"/> whose uplink fate (and echo, when <paramref name="echo"/>) is still unknown.</summary>
    public (int UplinkPending, int EchoPending) Pending(int step, bool echo)
    {
        lock (_sync)
        {
            if (step < 0 || step >= _steps.Count) return (0, 0);
            var log = _steps[step];
            var uplink = _byTransport.Values.Count(x => x.Step == step);
            var echoes = echo ? log.Sent - log.Echoes.Count : 0;
            return (uplink, echoes);
        }
    }

    /// <summary>The verdict on one step from what has been reported so far.</summary>
    public ProbeStep Judge(int step, bool stamped, bool echo)
    {
        lock (_sync)
        {
            var log = _steps[step];
            var (upKbps, upGrowth) = Delivered(log.Arrivals);
            var (echoKbps, echoGrowth) = Delivered(log.Echoes);
            var reported = log.Arrivals.Count + log.Lost;
            // Bytes after the first over the span they were sent in: what this device really offered.
            var sendSpanUs = log.LastSentUs - log.FirstSentUs;
            var sentKbps = log.Sent >= 4 && sendSpanUs >= 40_000
                ? (int)Math.Round((log.SentBytes - log.SentBytes / log.Sent) * 8.0 / (sendSpanUs / 1000.0)) : 0;
            var tested = sentKbps > 0 ? Math.Min(log.OfferedKbps, sentKbps) : log.OfferedKbps;
            var upPassed = stamped && reported > 0 && log.Lost <= Math.Max(2, reported * MaxLossShare) &&
                           (upKbps == 0 || upKbps >= tested * MinDeliveredShare) && upGrowth < MaxDelayGrowthMs &&
                           reported >= log.Sent * 0.9;
            // The echo crosses both legs: its loss is up to twice the uplink's, and it can only bring back what got there.
            var echoPassed = echo && log.Sent > 0 && log.Echoes.Count >= Math.Max(1, log.Sent * (1 - 2 * MaxLossShare) - 2) &&
                             (echoKbps == 0 || echoKbps >= Math.Min(tested, upKbps > 0 ? upKbps : tested) * MinDeliveredShare) &&
                             echoGrowth < MaxEchoGrowthMs;
            return new ProbeStep(log.OfferedKbps, log.Sent, upKbps, log.Lost, upGrowth, echoKbps, log.Echoes.Count, echoGrowth, upPassed, echoPassed, echo,
                sentKbps);
        }
    }

    /// <summary>
    /// The probe's conclusion. The uplink is the highest rate it carried, or, past that, what it delivered of the step
    /// that failed (a link at its limit delivers its capacity). The downlink comes from the echo: where the round trip
    /// failed on a step the uplink carried, the downlink is the limit; where the uplink failed first, the echo only says
    /// the downlink takes at least what came back.
    /// </summary>
    public static LinkProbeResult Conclude(IReadOnlyList<ProbeStep> steps, long durationMs, int? rttMs = null)
    {
        int up = 0, down = 0;
        bool upAtLeast = steps.Count > 0, downAtLeast = true, downKnown = false;
        foreach (var step in steps)
        {
            if (upAtLeast)
            {
                if (step.UplinkPassed) up = Math.Max(up, step.TestedKbps);
                else
                {
                    // A step that failed delivered what the link carries, never more than it was offered.
                    var delivered = step.UplinkKbps > 0 ? Math.Min(step.UplinkKbps, step.TestedKbps) : 0;
                    up = up > 0 ? Math.Max(up, delivered) : delivered > 0 ? delivered : step.TestedKbps / 2;
                    upAtLeast = false;
                }
            }
            if (!step.EchoRequested || !downAtLeast) continue;
            if (step.EchoPassed)
            {
                down = Math.Max(down, step.TestedKbps);
                downKnown = true;
                continue;
            }
            var back = step.EchoKbps > 0 ? Math.Min(step.EchoKbps, step.TestedKbps) : 0;
            if (step.Echoed > 0 || step.UplinkPassed) downKnown = true;
            down = Math.Max(down, back);
            // Only a round trip that failed where the uplink did not is the downlink's own limit; where the uplink failed
            // too, what came back is all the downlink was offered.
            if (step.UplinkPassed) downAtLeast = false;
            else break;
        }
        return new LinkProbeResult(up, upAtLeast, downKnown && down > 0 ? down : null, downAtLeast, rttMs, steps, durationMs);
    }

    /// <summary>
    /// Bytes after the first over the span of arrivals, and how much the one-way (or round-trip) delay of the last
    /// quarter of messages exceeds the first quarter's. Clocks need not agree: only differences of delay are used.
    /// </summary>
    private static (int Kbps, int GrowthMs) Delivered(List<(long SentUs, long ArrivalUs, int Bytes)> arrivals)
    {
        if (arrivals.Count < 4) return (0, 0);
        var ordered = arrivals.OrderBy(x => x.SentUs).ToArray();
        var first = ordered.Min(x => x.ArrivalUs);
        var last = ordered.Max(x => x.ArrivalUs);
        var bytes = ordered.Sum(x => (long)x.Bytes) - ordered.MinBy(x => x.ArrivalUs).Bytes;
        var spanUs = last - first;
        // Shorter than this, the arrivals are one burst: the link was not the limit, nothing to divide by.
        var kbps = spanUs >= 40_000 ? (int)Math.Round(bytes * 8.0 / (spanUs / 1000.0)) : 0;
        var quarter = Math.Max(1, ordered.Length / 4);
        double Delay((long SentUs, long ArrivalUs, int Bytes) x) => (x.ArrivalUs - x.SentUs) / 1000.0;
        var early = ordered.Take(quarter).Average(Delay);
        var late = ordered.Skip(ordered.Length - quarter).Average(Delay);
        return (kbps, (int)Math.Max(0, Math.Round(late - early)));
    }

    /// <summary>Smallest round trip the echoes showed, in ms.</summary>
    public int? MinRoundTripMs()
    {
        lock (_sync)
        {
            var all = _steps.SelectMany(x => x.Echoes).Select(x => (x.ArrivalUs - x.SentUs) / 1000.0).ToArray();
            return all.Length == 0 ? null : (int)Math.Round(all.Min());
        }
    }
}
