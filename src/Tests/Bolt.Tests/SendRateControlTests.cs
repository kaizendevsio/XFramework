using Bolt.Media.Congestion;
using Bolt.Protocol;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// Phase 1 of call network resilience: the sender's delay-based rate control, its picture ladder and its pacer.
/// The controller is driven through <see cref="LinkSimulation"/>, a bottleneck queue that reports the way the
/// relay does (queue delay, drained capacity, drops) after a feedback delay.
/// </summary>
[CancelAfter(20_000)]
public sealed class SendRateControlTests
{
    // ── Step responses ──

    [Test]
    public void AnOverloadOffer_LandsOnTheLinkWithinASecond_AndSettlesWithoutOscillating()
    {
        // A 720p offer (about 1.6 Mbps) on a 512 kbps link with 500 ms RTT.
        var sim = new LinkSimulation(capacityKbps: 450, oneWayMs: 250, startTier: 720);
        sim.Run(1_500);
        Assert.That(sim.Estimate, Is.LessThanOrEqualTo(450), "the first reports of a growing queue cut straight to the link");
        sim.Run(58_500);
        var settled = sim.Trace.Where(x => x.At >= 20_000).ToArray();
        Assert.Multiple(() =>
        {
            // AIMD around the link: probing past it slowly, then a cut, never a collapse.
            Assert.That(settled.Average(x => x.Estimate), Is.InRange(300, 500), "it uses most of the link");
            Assert.That(settled.Min(x => x.Estimate), Is.GreaterThan(300), "without cutting far below it");
            Assert.That(settled.Max(x => x.QueueMs), Is.LessThan(800), "and keeps the queue short");
            Assert.That(sim.RungChangesAfter(20_000), Is.LessThanOrEqualTo(2), "without flapping between picture sizes");
            Assert.That(settled.Any(x => x.Suspended), Is.False);
        });
    }

    [Test]
    public void A1080pOffer_On512kAt1000msRtt_StillConvergesInAFewSeconds()
    {
        var sim = new LinkSimulation(capacityKbps: 450, oneWayMs: 500, startTier: 1080);
        sim.Run(5_000);
        Assert.That(sim.Estimate, Is.LessThanOrEqualTo(450));
        sim.Run(55_000);
        var settled = sim.Trace.Where(x => x.At >= 20_000).ToArray();
        Assert.That(settled.Max(x => x.QueueMs), Is.LessThan(1_200));
        Assert.That(sim.RungChangesAfter(20_000), Is.LessThanOrEqualTo(2));
    }

    [Test]
    public void AGoodLink_RampsFrom240pToHighQuality()
    {
        var sim = new LinkSimulation(capacityKbps: 4_000, oneWayMs: 50, startTier: 240);
        sim.Run(60_000);
        Assert.Multiple(() =>
        {
            Assert.That(sim.Rung.Height, Is.GreaterThanOrEqualTo(720), "a 4 Mbps link carries at least 720p");
            Assert.That(sim.Trace.Where(x => x.At >= 30_000).Max(x => x.QueueMs), Is.LessThan(700));
            Assert.That(sim.RungChangesAfter(30_000), Is.Zero, "a keyframe burst does not cost the picture size");
        });
    }

    [Test]
    public void ABandwidthStepDown_IsFollowedWithinASecond_AndAStepUpIsProbedBackSlowly()
    {
        var sim = new LinkSimulation(capacityKbps: 4_000, oneWayMs: 50, startTier: 720);
        sim.Run(40_000);
        var before = sim.Estimate;
        Assert.That(before, Is.GreaterThan(1_500));
        sim.CapacityKbps = 480;
        sim.Run(1_500);
        Assert.That(sim.Estimate, Is.LessThanOrEqualTo(480), "fast down");
        sim.Run(28_500);
        Assert.That(sim.Trace.Where(x => x.At >= sim.Now - 15_000).Max(x => x.QueueMs), Is.LessThan(600));
        sim.CapacityKbps = 4_000;
        sim.Run(3_000);
        Assert.That(sim.Estimate, Is.LessThan(1_000), "slow up: no jump back to the old rate");
        sim.Run(57_000);
        Assert.That(sim.Estimate, Is.GreaterThan(1_500), "but it does find the room again");
    }

    [Test]
    public void ALinkTooSmallForVideo_KeepsVoice_AndBringsVideoBackWhenItRecovers()
    {
        var sim = new LinkSimulation(capacityKbps: 100, oneWayMs: 250, startTier: 360);
        sim.Run(20_000);
        Assert.That(sim.Suspended, Is.True, "video stands down so audio keeps the link");
        Assert.That(sim.Trace.Where(x => x.At >= 10_000).Average(x => x.QueueMs), Is.LessThan(500));
        sim.CapacityKbps = 1_000;
        sim.Run(60_000);
        Assert.That(sim.Suspended, Is.False, "and resumes by itself");
        Assert.That(sim.Rung.Height, Is.GreaterThanOrEqualTo(240));
    }

    [Test]
    public void ALinkThatStaysBad_BacksOffResumeAttempts()
    {
        var controller = new SendRateController(400);
        var now = 0L;
        var holds = new List<int>();
        var wasSuspended = false;
        for (; now < 180_000; now += 250)
        {
            // Every time video is back the queue explodes; with video off the link is calm.
            var delay = controller.VideoSuspended ? 20 : 900;
            var decision = controller.Update(new SendPathSample(now, controller.VideoSuspended ? 60 : 300, 60, delay, 0, false,
                new RelaySignal(now, delay, 0, 90, !controller.VideoSuspended)));
            if (decision.VideoSuspended && !wasSuspended) holds.Add(controller.ResumeHoldMs);
            wasSuspended = decision.VideoSuspended;
        }
        Assert.That(holds.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(holds.Last(), Is.GreaterThan(holds.First()), "each failed resume waits longer");
        Assert.That(holds.Count, Is.LessThan(12), "and it does not flap");
    }

    [Test]
    public void NoNewsFromDownstream_IsNotGoodNews()
    {
        var controller = new SendRateController(300);
        long now = 0;
        // A relay reported once, then went silent (a dead link): the estimate must not climb.
        controller.Update(new SendPathSample(now, 300, 60, 10, 0, false, new RelaySignal(now, 10, 0, 0, false)));
        for (now = 250; now < 30_000; now += 250)
            controller.Update(new SendPathSample(now, 300, 60, 10, 0, false, new RelaySignal(0, 10, 0, 0, false)));
        Assert.That(controller.EstimateKbps, Is.LessThanOrEqualTo(330), "at most the one step it took while the report was fresh");
    }

    [Test]
    public void AudioStaysRobust_UnderCongestion_AndGoesHigherOnlyWithClearHeadroom()
    {
        var tight = new SendRateController(200);
        var decision = tight.Update(new SendPathSample(0, 200, 60, 10, 0, false));
        Assert.That(decision.AudioKbps, Is.EqualTo(24));

        var roomy = new SendRateController(3_000);
        SendRateDecision last = default;
        for (long now = 0; now < 40_000; now += 250)
            last = roomy.Update(new SendPathSample(now, 2_900, 90, 5, 0, false, new RelaySignal(now, 5, 0, 0, false)));
        Assert.That(last.AudioKbps, Is.EqualTo(40));
        Assert.That(new SendRateController(800).Update(new SendPathSample(0, 800, 80, 5, 0, false)).AudioKbps, Is.EqualTo(32));
    }

    [Test]
    public void ADrainingQueue_IsNotCutAgain()
    {
        var controller = new SendRateController(1_600);
        long now = 0;
        // The relay lost base pictures: a cut straight to the link it measured.
        controller.Update(new SendPathSample(now, 1_600, 80, 900, 0, false, new RelaySignal(now, 900, 0, 450, true, BaseLost: true)));
        var afterCut = controller.EstimateKbps;
        // The queue now shrinks steadily: the cut is working.
        for (var delay = 880; delay > 300; delay -= 40)
        {
            now += 250;
            controller.Update(new SendPathSample(now, 380, 80, delay, 0, false, new RelaySignal(now, delay, 0, 450, false)));
        }
        Assert.That(controller.EstimateKbps, Is.EqualTo(afterCut));
    }

    [Test, Explicit("prints simulation traces for tuning")]
    public void PrintTraces()
    {
        foreach (var (capacity, oneWay, tier) in new[] { (450, 250, 720), (450, 500, 1080), (4_000, 50, 240), (130, 500, 720) })
        {
            var sim = new LinkSimulation(capacity, oneWay, tier);
            sim.Run(60_000);
            TestContext.Out.WriteLine($"== capacity {capacity} one-way {oneWay} start {tier}p");
            TestContext.Out.WriteLine(sim.ToString());
        }
    }

    // ── Ladder ──

    [Test]
    public void Ladder_DropsStraightToTheRungThatFits_ButClimbsOneStepAfterAHold()
    {
        var ladder = new VideoRateLadder(VideoRateLadder.IndexForHeight(1080));
        var down = ladder.Place(300, 0, congested: true);
        Assert.That(down!.Value.Rung.Height, Is.EqualTo(360), "one decision, several rungs");
        Assert.That(ladder.Place(2_000, 100, congested: false)?.Rung.Height ?? 360, Is.EqualTo(360), "raise the bitrate first");
        ladder.Place(2_000, 2_700, congested: false);
        Assert.That(ladder.Current.Rung.Height, Is.EqualTo(540), "one rung after the hold");
    }

    [Test]
    public void Ladder_HasAGapBetweenItsDownAndUpThresholds()
    {
        var ladder = new VideoRateLadder(VideoRateLadder.IndexForHeight(360));
        var rung = VideoRateLadder.Rungs[VideoRateLadder.IndexForHeight(540)];
        // Just above 540p's minimum is not enough to climb there...
        for (long now = 0; now < 10_000; now += 250) ladder.Place(rung.MinKbps + 10, now, false);
        Assert.That(ladder.Current.Rung.Height, Is.EqualTo(360));
        // ...but once there, the same budget keeps it there.
        for (long now = 10_000; now < 14_000; now += 250) ladder.Place(rung.UpKbps + 10, now, false);
        Assert.That(ladder.Current.Rung.Height, Is.EqualTo(540));
        for (long now = 14_000; now < 24_000; now += 250) ladder.Place(rung.MinKbps + 10, now, false);
        Assert.That(ladder.Current.Rung.Height, Is.EqualTo(540));
    }

    [Test]
    public void Ladder_Uses60FpsOnlyOnTheTopRungWithRoomToSpare_AndDropsItFirst()
    {
        var ladder = new VideoRateLadder(VideoRateLadder.IndexForHeight(720), allow60: true);
        ladder.SetCeiling(720, allow60: true);
        for (long now = 0; now < 3_000; now += 250) ladder.Place(1_400, now, false);
        Assert.That(ladder.Current.Rung.Framerate, Is.EqualTo(30), "1.4 Mbps does not pay for 720p60");
        for (long now = 3_000; now < 7_000; now += 250) ladder.Place(2_600, now, false);
        Assert.That(ladder.Current.Rung, Is.EqualTo(new VideoRung(1280, 720, 60, 1_500, 3_900)));
        ladder.Place(1_200, 7_250, true);
        Assert.That(ladder.Current.Rung.Framerate, Is.EqualTo(30), "frame rate goes before resolution");
        Assert.That(ladder.Current.Rung.Height, Is.EqualTo(720));

        var noPreference = new VideoRateLadder(VideoRateLadder.IndexForHeight(720), allow60: false);
        noPreference.SetCeiling(720, allow60: false);
        for (long now = 0; now < 10_000; now += 250) noPreference.Place(10_000, now, false);
        Assert.That(noPreference.Current.Rung.Framerate, Is.EqualTo(30), "the user's preference is the ceiling");
    }

    [Test]
    public void Ladder_ACpuBacklogCapsThePictureEvenWithBandwidthToSpare()
    {
        var ladder = new VideoRateLadder(VideoRateLadder.IndexForHeight(1080));
        Assert.That(ladder.ReduceForCpu(), Is.True);
        for (long now = 0; now < 20_000; now += 250) ladder.Place(20_000, now, false);
        Assert.That(ladder.Current.Rung.Height, Is.EqualTo(900));
    }

    // ── Simulation ──

    /// <summary>
    /// A sender (pacer-free: its output is the controller's allocation) into a bottleneck queue drained at
    /// <see cref="CapacityKbps"/>, with a relay report every 250 ms that reaches the sender after one one-way
    /// delay. Keyframes are bursts, and the relay drops video when its queue passes 1.5 s, like the real one.
    /// </summary>
    private sealed class LinkSimulation
    {
        private readonly SendRateController _controller;
        private readonly VideoRateLadder _ladder;
        private readonly Queue<(long At, RelaySignal Signal)> _inFlight = new();
        private readonly int _oneWayMs;
        private double _queueBytes;
        private double _drained;
        private int _sentWindowBytes, _audioWindowBytes;
        private RelaySignal? _relay;
        private long _nextReport, _nextTick;
        private double _nextPicture;
        private int _pictures;
        private bool _dropping;
        private VideoRung _lastRung;
        private readonly List<long> _rungChanges = [];

        public LinkSimulation(int capacityKbps, int oneWayMs, int startTier)
        {
            CapacityKbps = capacityKbps;
            _oneWayMs = oneWayMs;
            _ladder = new VideoRateLadder(VideoRateLadder.IndexForHeight(startTier));
            var start = _ladder.Current.Rung;
            _ladder.Place((start.MinKbps + start.MaxKbps) / 2, 0, true);
            _lastRung = _ladder.Current.Rung;
            _controller = new SendRateController(_ladder.Current.BitrateKbps + 80);
        }

        public int CapacityKbps { get; set; }
        public long Now { get; private set; }
        public int Estimate => _controller.EstimateKbps;
        public bool Suspended => _controller.VideoSuspended;
        public VideoRung Rung => _ladder.Current.Rung;
        public List<(long At, int Estimate, int QueueMs, bool Suspended, VideoSetting Rung)> Trace { get; } = [];
        public int RungChangesAfter(long at) => _rungChanges.Count(x => x >= at);

        public void Run(long milliseconds)
        {
            var end = Now + milliseconds;
            for (; Now < end; Now += 10)
            {
                // Audio: 20 ms packets of Opus 32k plus overhead.
                if (Now % 20 == 0) Offer(80 + 130, audio: true);
                // Video at the ladder's bitrate and frame rate, a keyframe (6x) every 10 s.
                var rung = _ladder.Current.Rung;
                if (!_controller.VideoSuspended && Now >= _nextPicture)
                {
                    _nextPicture = Math.Max(_nextPicture + 1000.0 / rung.Framerate, Now);
                    var perPicture = _ladder.Current.BitrateKbps * 1000 / 8.0 / rung.Framerate;
                    var key = _pictures++ % (rung.Framerate * 10) == 0;
                    if (!_dropping) Offer((int)(perPicture * (key ? 6 : 0.9)), audio: false);
                }
                // Drain the bottleneck.
                var drain = Math.Min(_queueBytes, CapacityKbps * 10 / 8.0);
                _queueBytes -= drain;
                _drained += drain;
                var queueMs = (int)(_queueBytes * 8 / CapacityKbps);
                _dropping = queueMs > 1_500;
                if (_dropping) _queueBytes = Math.Min(_queueBytes, CapacityKbps * 1_000 / 8.0);

                if (Now >= _nextReport)
                {
                    _nextReport = Now + 250;
                    var limited = queueMs >= 40;
                    _inFlight.Enqueue((Now + _oneWayMs, new RelaySignal(0, queueMs, 0, limited ? CapacityKbps : 0, _dropping, BaseLost: _dropping)));
                }
                while (_inFlight.Count > 0 && _inFlight.Peek().At <= Now)
                {
                    var (_, signal) = _inFlight.Dequeue();
                    _relay = signal with { ReceivedAtMs = Now };
                }

                if (Now >= _nextTick)
                {
                    _nextTick = Now + SendRateLoop.IntervalMs;
                    var sent = _sentWindowBytes * 8 / SendRateLoop.IntervalMs;
                    var audio = _audioWindowBytes * 8 / SendRateLoop.IntervalMs;
                    _sentWindowBytes = _audioWindowBytes = 0;
                    var decision = _controller.Update(new SendPathSample(Now, sent, audio, 0, 0, false, _relay));
                    if (!decision.VideoSuspended)
                        _ladder.Place(decision.VideoKbps, Now, decision.Signal != RateSignal.Normal);
                    if (_ladder.Current.Rung != _lastRung) { _rungChanges.Add(Now); _lastRung = _ladder.Current.Rung; }
                    Trace.Add((Now, decision.TotalKbps, queueMs, decision.VideoSuspended, _ladder.Current));
                }
            }
        }

        private void Offer(int bytes, bool audio)
        {
            _queueBytes += bytes;
            _sentWindowBytes += bytes;
            if (audio) _audioWindowBytes += bytes;
        }

        public override string ToString() => string.Join("\n", Trace.Where((_, i) => i % 4 == 0)
            .Select(x => $"{x.At / 1000.0,6:F1}s E={x.Estimate,5} q={x.QueueMs,5} {x.Rung} {(x.Suspended ? "SUSP" : "")}"));
    }
}
