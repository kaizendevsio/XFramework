using Bolt.Media.Congestion;
using Bolt.Protocol;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// A call's picture starts at the size its link carries, not at the bottom of the ladder: the start probe
/// (<see cref="LinkProbe"/>), the start-rate choice (<see cref="StartRate"/>), the picture start and its revisions
/// (<see cref="PictureStart"/>), the controller's start-up ramp and the ladder's fast climb. The end-to-end behaviour is
/// checked through <see cref="StartSimulation"/>, a bottleneck that reports the way the relay does.
/// </summary>
[CancelAfter(20_000)]
public sealed class CallFastStartTests
{
    private const int AudioWire = 84; // Opus 32k plus framing, as the browser client counts it.

    // ── The probe's judgement ──

    [Test]
    public void Probe_AFastLink_CarriesEveryStep_AndIsALowerBoundBothWays()
    {
        var result = ProbeThrough(upKbps: 20_000, downKbps: 20_000);
        Assert.Multiple(() =>
        {
            Assert.That(result.Steps, Has.Count.EqualTo(LinkProbe.DefaultStepsKbps.Length));
            Assert.That(result.UplinkKbps, Is.EqualTo(LinkProbe.DefaultStepsKbps[^1]));
            Assert.That(result.UplinkAtLeast, Is.True, "the link's limit lies above what the probe offered");
            Assert.That(result.DownlinkKbps, Is.EqualTo(LinkProbe.DefaultStepsKbps[^1]));
            Assert.That(result.DownlinkAtLeast, Is.True);
        });
    }

    [Test]
    public void Probe_A512kUplink_StopsAtTheFirstStepItCannotCarry_AndMeasuresIt()
    {
        var result = ProbeThrough(upKbps: 512, downKbps: 20_000);
        Assert.Multiple(() =>
        {
            Assert.That(result.Steps, Has.Count.EqualTo(1), "the 600 kbps step already built a queue: no larger burst follows");
            Assert.That(result.UplinkAtLeast, Is.False);
            Assert.That(result.UplinkKbps, Is.InRange(430, 560), "what the saturated link delivered is its capacity");
        });
    }

    [Test]
    public void Probe_ASlowDownlink_IsFoundByTheEcho_WhileTheUplinkKeepsClimbing()
    {
        var result = ProbeThrough(upKbps: 20_000, downKbps: 1_500);
        TestContext.Out.WriteLine(result);
        Assert.Multiple(() =>
        {
            Assert.That(result.UplinkAtLeast, Is.True, "the uplink carried every step");
            Assert.That(result.DownlinkAtLeast, Is.False, "the round trip failed on a step the uplink carried: that is the downlink's limit");
            Assert.That(result.DownlinkKbps, Is.InRange(1_200, 1_600));
            Assert.That(result.Steps.Count(x => x.EchoRequested && !x.EchoPassed), Is.EqualTo(2), "two failed round trips in a row confirm it");
        });
    }

    [Test]
    public void Probe_OneFailedRoundTrip_IsNotALimit_UntilASecondConfirmsIt()
    {
        // A busy page (WebKit starting its own picture) failed the first round trip on its own; the next one passed.
        ProbeStep Step(int kbps, bool echoPassed, int echoKbps) =>
            new(kbps, 30, kbps, 0, 0, echoKbps, 30, echoPassed ? 0 : 50, UplinkPassed: true, EchoPassed: echoPassed);
        var noise = LinkProbe.Conclude([Step(600, false, 455), Step(1_500, true, 1_500), Step(3_500, true, 3_500)], 0);
        Assert.That((noise.DownlinkKbps, noise.DownlinkAtLeast), Is.EqualTo(((int?)3_500, true)));
        var alone = LinkProbe.Conclude([Step(600, true, 600), Step(1_500, false, 900)], 0);
        Assert.That(alone.DownlinkAtLeast, Is.True, "an unconfirmed failure at the end is not a limit either");
        var real = LinkProbe.Conclude([Step(600, true, 600), Step(1_500, false, 1_200), Step(3_500, false, 1_450)], 0);
        Assert.That((real.DownlinkKbps, real.DownlinkAtLeast), Is.EqualTo(((int?)1_450, false)));
    }

    [Test]
    public void Probe_RandomLossOfAMobileLink_IsNotALimit()
    {
        var result = ProbeThrough(upKbps: 20_000, downKbps: 20_000, lossEvery: 100);
        Assert.That(result.UplinkAtLeast, Is.True);
    }

    [Test]
    public void Probe_ARelayThatDoesNotEcho_LeavesTheDownlinkUnknown()
    {
        var result = ProbeThrough(upKbps: 20_000, downKbps: 20_000, echo: false);
        Assert.That(result.DownlinkKbps, Is.Null);
        Assert.That(result.UplinkAtLeast, Is.True);
    }

    [Test]
    public void Probe_AStepTheDeviceHeldBack_IsALowerBound_NotTheLinksLimit()
    {
        // The browser's own channel let only 16 messages of a 3500 kbit/s step out (a fresh association's slow start).
        var probe = new LinkProbe();
        var step = probe.BeginStep(3_500);
        var arrivals = new List<long>();
        for (uint index = 0; index < 16; index++)
        {
            var sent = index * 12_500L; // ~700 kbit/s of 1100-byte messages
            probe.OnSent(step, index, (ushort)index, 1_100, sent);
            arrivals.Add(sent + 10_000);
        }
        probe.OnFeedback(0, arrivals);
        var verdict = probe.Judge(step, stamped: true, echo: false);
        var result = LinkProbe.Conclude([verdict], 0);
        Assert.Multiple(() =>
        {
            Assert.That(verdict.HeldBack, Is.True);
            Assert.That(verdict.UplinkPassed, Is.True, "the link carried all it was given");
            Assert.That(result.UplinkAtLeast, Is.True, "so it is a lower bound, never a limit");
            Assert.That(result.UplinkKbps, Is.InRange(600, 800));
        });
    }

    [Test]
    public void ABrowserChannelThatStalls_IsCutGently_NotCollapsed()
    {
        // WebKit holds 280 KB in its channel for two seconds while the relay, the uplink and the receiver see no queue.
        var controller = new SendRateController(2_100);
        for (long now = 0; now <= 2_000; now += 250)
            controller.Update(new SendPathSample(now, 35, 30, 3_000 + (int)now, 40, now == 500, Transport: new TransportSignal(now, 2, 0, 0, 600),
                Receiver: new ReceiverSignal(now, 20, 900)));
        Assert.That(controller.EstimateKbps, Is.GreaterThan(2_100 / 3), "a few gentle cuts, not 48 kbit/s");
        Assert.That(controller.VideoSuspended, Is.False);
    }

    [Test]
    public void Probe_AHeldBackStepThatDeliveredLessThanItSent_IsStillALowerBound()
    {
        // WebKit sent 2.7 of a 3.5 Mbit/s step and the relay saw it arrive in bursts (about 2.3 Mbit/s over the span),
        // with no queue building: the browser's pace, not the link's limit.
        var probe = new LinkProbe();
        var step = probe.BeginStep(3_500);
        var arrivals = new List<long>();
        for (uint index = 0; index < 54; index++)
        {
            probe.OnSent(step, index, (ushort)index, 1_100, index * 3_200L);
            arrivals.Add(10_000 + index * 3_200L + (index % 4) * 1_500L + (index == 53 ? 40_000 : 0));
        }
        probe.OnFeedback(0, arrivals);
        var verdict = probe.Judge(step, stamped: true, echo: false);
        var result = LinkProbe.Conclude([verdict], 0);
        Assert.Multiple(() =>
        {
            Assert.That(verdict.HeldBack, Is.True);
            Assert.That(verdict.UplinkKbps, Is.LessThan(verdict.SentKbps * LinkProbe.MinDeliveredShare), "it did deliver less than it sent");
            Assert.That(result.UplinkAtLeast, Is.True, "never the link's limit: the start-up ramp, not the probe, finds it");
        });
    }

    [Test]
    public void Probe_ReportsThatDoNotComeBack_SayNothing()
    {
        // The relay's feedback rides this device's downlink, which the other side's picture already fills.
        var probe = new LinkProbe();
        var step = probe.BeginStep(600);
        for (uint index = 0; index < 13; index++) probe.OnSent(step, index, (ushort)index, 1_100, index * 15_000L);
        probe.OnFeedback(0, [10_000, 25_000]);
        var verdict = probe.Judge(step, stamped: true, echo: false);
        var result = LinkProbe.Conclude([verdict], 0);
        Assert.That(verdict.Inconclusive, Is.True);
        Assert.That(result.UplinkKbps, Is.Zero, "unknown: the start falls back to the last call or a middle picture, not 300 kbit/s");

        // Late reports that already show a queue are the uplink's own (a 512 kbit/s link queued them as well).
        var slow = new LinkProbe();
        var first = slow.BeginStep(600);
        var arrivals = new List<long>();
        for (uint index = 0; index < 13; index++)
        {
            slow.OnSent(first, index, (ushort)index, 1_100, index * 15_000L);
            if (index < 10) arrivals.Add(10_000 + index * 22_000L); // drained at about 400 kbit/s: the delay grows
        }
        slow.OnFeedback(0, arrivals);
        var judged = slow.Judge(first, stamped: true, echo: false);
        Assert.That((judged.Inconclusive, judged.UplinkPassed), Is.EqualTo((false, false)));
        Assert.That(LinkProbe.Conclude([judged], 0).UplinkKbps, Is.InRange(300, 500));
    }

    [Test]
    public void APictureGrowsOnlyWhileTheReceiversKeepUp()
    {
        var controller = new SendRateController(2_000);
        Assert.That(controller.Update(new SendPathSample(0, 2_000, 80, 0, 0, false)).ReceiversCalm, Is.True, "nobody has reported yet");
        Assert.That(controller.Update(new SendPathSample(250, 2_000, 80, 0, 0, false, Receiver: new ReceiverSignal(250, 900, 1_000))).ReceiversCalm,
            Is.False, "a receiver whose page is still starting reports 900 ms");
        Assert.That(controller.Update(new SendPathSample(500, 2_000, 80, 0, 0, false, Receiver: new ReceiverSignal(500, 20, 1_900))).ReceiversCalm, Is.True);
    }

    [Test]
    public void PaddingAndDownlinkFeedback_RoundTrip_AndOldReadersIgnoreTheExtension()
    {
        var padding = new byte[200];
        PaddingCodec.Write(padding, PaddingCodec.Echo, 3, 77);
        Assert.That(PaddingCodec.TryRead(padding, out var flags, out var step, out var index), Is.True);
        Assert.That((flags, step, index), Is.EqualTo((PaddingCodec.Echo, (byte)3, 77u)));
        Assert.That(padding.Skip(PaddingCodec.HeaderSize).All(x => x == 0), "padding carries nothing");
        Assert.That(Bolt.Protocol.Transport.DatagramFramePolicy.AcceptFromParticipant(FrameType.Padding), Is.True);

        var writer = new System.Buffers.ArrayBufferWriter<byte>();
        BoltCodec.WriteMediaFeedback(writer, Guid.NewGuid(), 10, 0, 0, 0, QualityHint.Maintain, ((ushort)12, 900u), (1_500u, false));
        Assert.That(BoltCodec.TryReadMediaFeedback(writer.WrittenSpan, out var feedback), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(writer.WrittenCount, Is.EqualTo(BoltCodec.MediaFeedbackDownlinkSize));
            Assert.That(feedback.HasDelayReport, Is.True);
            Assert.That((feedback.QueueDelayMs, feedback.ReceivedKbps), Is.EqualTo(((ushort)12, 900u)));
            Assert.That((feedback.DownlinkKbps, feedback.DownlinkAtLeast), Is.EqualTo((1_500u, false)));
        });
        // A downlink without a delay report (nothing received yet), and an old 40-byte reader's view of the frame.
        writer.Clear();
        BoltCodec.WriteMediaFeedback(writer, Guid.NewGuid(), 0, 0, 0, 0, QualityHint.Maintain, null, (7_500u, true));
        Assert.That(BoltCodec.TryReadMediaFeedback(writer.WrittenSpan, out feedback), Is.True);
        Assert.That((feedback.HasDelayReport, feedback.DownlinkKbps, feedback.DownlinkAtLeast), Is.EqualTo((false, 7_500u, true)));
        Assert.That(BoltCodec.TryReadMediaFeedback(writer.WrittenSpan[..BoltCodec.MediaFeedbackExtendedSize], out feedback), Is.True);
        Assert.That(feedback.DownlinkKbps, Is.Zero);
    }

    // ── Where a picture starts ──

    [TestCase(7_500, true, null, false, null, null, 1080, StartRate.UplinkProbe, TestName = "A fast uplink starts at the preference")]
    [TestCase(500, false, null, false, null, null, 360, StartRate.UplinkProbe, TestName = "A 512k uplink starts at 360p")]
    [TestCase(7_500, true, 1_500, false, null, null, 540, StartRate.ReceiverDownlink, TestName = "The worst receiver's downlink bounds the start")]
    [TestCase(7_500, true, 1_500, true, null, null, 1080, StartRate.UplinkProbe, TestName = "A receiver's lower bound does not")]
    [TestCase(null, false, null, false, null, null, 540, StartRate.Default, TestName = "Nothing known starts at a middle picture")]
    [TestCase(null, false, null, false, 5_000, null, 1080, StartRate.LastCall, TestName = "The last call on this network is remembered")]
    [TestCase(null, false, null, false, null, 700, 360, StartRate.NetworkHint, TestName = "A connection the browser calls 3G caps it")]
    public void Start_PlacesThePictureOnWhatIsKnown(int? up, bool upAtLeast, int? down, bool downAtLeast, int? cached, int? cap,
        int height, string source)
    {
        var hints = new StartHints(up, upAtLeast, down, downAtLeast, cached, cap);
        var estimate = StartRate.Choose(hints, AudioWire, 48, 40_000);
        var ladder = new VideoRateLadder(1);
        ladder.SetCeiling(1080, allow60: false);
        var setting = ladder.Start(estimate.TotalKbps - AudioWire);
        Assert.That(setting.Rung.Height, Is.EqualTo(height), estimate.ToString());
        Assert.That(estimate.Source, Is.EqualTo(source));
        Assert.That(setting.BitrateKbps, Is.LessThanOrEqualTo(estimate.TotalKbps - AudioWire), "the first keyframe is sized to the estimate");
    }

    [Test]
    public void Ladder_AFreshPictureClimbsSeveralRungsAtOnce_ButOnlyUntilItFirstComesDown()
    {
        var ladder = new VideoRateLadder(VideoRateLadder.IndexForHeight(540));
        ladder.SetCeiling(1080, false);
        ladder.Start(900);
        Assert.That(ladder.Place(4_000, 0, congested: false)?.Rung.Height ?? 540, Is.EqualTo(540), "the budget has to hold first");
        Assert.That(ladder.Place(4_000, VideoRateLadder.FastUpHoldMs, congested: false)?.Rung.Height, Is.EqualTo(1080), "then straight to the top: one keyframe, not three");
        ladder.Place(1_000, VideoRateLadder.FastUpHoldMs + 250, congested: true);
        Assert.That(ladder.Fresh, Is.False, "the first step down ends the fast climb");
        // A size that came down right after going up doubles the backoff.
        for (long now = 2_000; now < 2_000 + 2 * VideoRateLadder.UpBackoffMs + VideoRateLadder.UpHoldMs; now += 250) ladder.Place(4_000, now, congested: false);
        Assert.That(ladder.Current.Rung.Height, Is.EqualTo(900), "from then on: one rung at a time, after the backoff and the full hold");
    }

    [Test]
    public void Ladder_Starts60FpsOnlyOnTheCeilingRung_WithTheBudgetForIt()
    {
        var ladder = new VideoRateLadder(1, allow60: true);
        ladder.SetCeiling(720, allow60: true);
        Assert.That(ladder.Start(3_000).Rung, Is.EqualTo(new VideoRung(1280, 720, 60, 1_500, 3_900)));
        Assert.That(ladder.Start(1_500).Rung.Framerate, Is.EqualTo(30));
    }

    // ── Whole starts through a bottleneck ──

    [Test]
    public void AFastLink_WithAProbe_StartsAtThePreference()
    {
        var sim = new StartSimulation(capacityKbps: 20_000, oneWayMs: 15, preferredHeight: 1080);
        sim.Begin(new StartHints(UplinkKbps: 7_500, UplinkAtLeast: true));
        sim.Run(20_000);
        Assert.Multiple(() =>
        {
            Assert.That(sim.FirstHeight, Is.EqualTo(PictureStart.FirstPictureMaxHeight), "a 720p first keyframe into channels that just opened");
            Assert.That(sim.TimeToHeightMs(1080), Is.LessThanOrEqualTo(1_250), "then 1080p within about a second");
            Assert.That(sim.RungChanges, Is.EqualTo(1), "and it stays there");
        });
    }

    [Test]
    public void AFastLink_WithNothingKnown_ReachesThePreferenceWithinFourSeconds()
    {
        var sim = new StartSimulation(capacityKbps: 20_000, oneWayMs: 15, preferredHeight: 1080);
        sim.Begin(new StartHints());
        sim.Run(20_000);
        TestContext.Out.WriteLine(sim);
        Assert.Multiple(() =>
        {
            Assert.That(sim.FirstHeight, Is.EqualTo(540), "a middle picture, not 240p");
            Assert.That(sim.TimeToHeightMs(1080), Is.LessThanOrEqualTo(4_000), "the start-up ramp, not half a minute");
            Assert.That(sim.RungChanges, Is.LessThanOrEqualTo(2), "one or two jumps up, nothing down");
            Assert.That(sim.MaxQueueMs(0, 20_000), Is.LessThan(150));
        });
    }

    [Test]
    public void A512kLink_StartsAtAFittingPicture_WithoutAQueueOrALargeKeyframe()
    {
        var sim = new StartSimulation(capacityKbps: 512, oneWayMs: 50, preferredHeight: 1080);
        sim.Begin(new StartHints(UplinkKbps: 500, UplinkAtLeast: false));
        sim.Run(30_000);
        TestContext.Out.WriteLine(sim);
        Assert.Multiple(() =>
        {
            Assert.That(sim.FirstHeight, Is.LessThanOrEqualTo(360));
            Assert.That(sim.LargestKeyframeBytes, Is.LessThan(512_000 / 8 / 2), "the first keyframe crosses the link in under half a second");
            Assert.That(sim.MaxQueueMs(0, 10_000), Is.LessThan(250), "no stall: the first keyframe is the only queue");
            Assert.That(sim.MaxQueueMs(10_000, 30_000), Is.LessThan(800), "and later probing past the link stays the usual AIMD");
            Assert.That(sim.Suspended, Is.False);
            Assert.That(sim.RungChangesAfter(5_000), Is.LessThanOrEqualTo(1), "no flapping");
        });
    }

    [Test]
    public void A512kLink_WithNothingKnown_LandsOnTheLinkWithinTwoSeconds()
    {
        var sim = new StartSimulation(capacityKbps: 512, oneWayMs: 50, preferredHeight: 1080);
        sim.Begin(new StartHints());
        sim.Run(30_000);
        TestContext.Out.WriteLine(sim);
        Assert.Multiple(() =>
        {
            Assert.That(sim.EstimateAt(2_000), Is.LessThanOrEqualTo(512), "the middle start is too big here; fast down fixes it");
            Assert.That(sim.MaxQueueMs(5_000, 30_000), Is.LessThan(800));
            Assert.That(sim.RungChangesAfter(10_000), Is.LessThanOrEqualTo(2));
        });
    }

    [Test]
    public void AStepDownMidCall_IsStillFollowedWithinASecondAndAHalf()
    {
        var sim = new StartSimulation(capacityKbps: 20_000, oneWayMs: 15, preferredHeight: 1080);
        sim.Begin(new StartHints(UplinkKbps: 7_500, UplinkAtLeast: true));
        sim.Run(30_000);
        sim.CapacityKbps = 1_500;
        sim.Run(1_500);
        Assert.That(sim.Estimate, Is.LessThanOrEqualTo(1_500), "fast down");
        sim.Run(28_500);
        TestContext.Out.WriteLine(sim);
        Assert.Multiple(() =>
        {
            Assert.That(sim.MaxQueueMs(sim.Now - 20_000, sim.Now), Is.LessThan(500));
            Assert.That(sim.RungChangesAfter(sim.Now - 20_000), Is.LessThanOrEqualTo(2), "no flapping on the smaller link");
        });
    }

    [Test]
    public void AReceiverDownlinkThatArrivesLate_LowersTheStartAtOnce_AndCapsTheRamp()
    {
        var sim = new StartSimulation(capacityKbps: 20_000, oneWayMs: 15, preferredHeight: 1080);
        sim.Begin(new StartHints(UplinkKbps: 7_500, UplinkAtLeast: true));
        sim.Run(1_500);
        Assert.That(sim.Height, Is.EqualTo(1080));
        // The receiver's own probe finished: its downlink carries 1.5 Mbit/s.
        sim.ReceiversDownlink = (1_500, false);
        sim.Run(500);
        Assert.That(sim.Estimate, Is.LessThanOrEqualTo(1_500), "under the receiver's limit before any queue could say so");
        Assert.That(sim.Height, Is.LessThanOrEqualTo(720));
        sim.Run(8_000);
        Assert.That(sim.Estimate, Is.LessThanOrEqualTo(1_500), "the start-up ramp stops at the receiver's measured limit");
    }

    [Test]
    public void AProbeThatLandsAfterThePictureStarted_LiftsItToThePreference()
    {
        var sim = new StartSimulation(capacityKbps: 20_000, oneWayMs: 15, preferredHeight: 1080);
        sim.Begin(new StartHints());
        sim.Run(300);
        sim.Start.UplinkMeasured(7_500, atLeast: true);
        sim.Run(2_000);
        Assert.That(sim.TimeToHeightMs(1080), Is.LessThanOrEqualTo(1_500));
    }

    [Test]
    public void AGoodButLimitedLink_RampsFastThenSettles_WithoutFlapping()
    {
        // Synthetic interframes undershoot the encoder target by 10%; 2.8 Mbit/s keeps this
        // limited-link scenario below the 1080p up-threshold even with that headroom.
        var sim = new StartSimulation(capacityKbps: 2_800, oneWayMs: 30, preferredHeight: 1080);
        sim.Begin(new StartHints());
        sim.Run(60_000);
        TestContext.Out.WriteLine(sim);
        Assert.Multiple(() =>
        {
            Assert.That(sim.Height, Is.InRange(720, 900), "most of a 2.8 Mbit/s link");
            Assert.That(sim.RungChangesAfter(15_000), Is.LessThanOrEqualTo(2), "no flapping");
            Assert.That(sim.MaxQueueMs(15_000, 60_000), Is.LessThan(800));
        });
    }

    [Test]
    public void AStartTransient_IsRecoveredFromInSeconds_ButALinkThatStaysSmallIsNotFlappedAt()
    {
        // The receiver's page is busy starting its own picture for 1.5 s: its reports say 900 ms of delay.
        var sim = new StartSimulation(capacityKbps: 20_000, oneWayMs: 15, preferredHeight: 1080);
        sim.Begin(new StartHints(UplinkKbps: 7_500, UplinkAtLeast: true));
        sim.ExtraDelay = (1_000, 2_500, 900);
        sim.Run(30_000);
        TestContext.Out.WriteLine(sim);
        Assert.Multiple(() =>
        {
            Assert.That(sim.EstimateAt(2_500), Is.LessThan(4_000), "it did react: the receiver was behind");
            Assert.That(sim.Height, Is.EqualTo(1080), "and is back at the preference");
            Assert.That(sim.LastRungChangeMs, Is.LessThanOrEqualTo(9_000), "within seconds, not half a minute");
            Assert.That(sim.RungChangesAfter(10_000), Is.Zero);
        });

        // The same start on a link that really is 2 Mbit/s (nothing measured it): one climb back, then it stays down.
        var small = new StartSimulation(capacityKbps: 2_000, oneWayMs: 15, preferredHeight: 1080);
        small.Begin(new StartHints(UplinkKbps: 7_500, UplinkAtLeast: true));
        small.Run(60_000);
        TestContext.Out.WriteLine(small);
        Assert.Multiple(() =>
        {
            Assert.That(small.RungChangesAfter(15_000), Is.LessThanOrEqualTo(2), "no flapping");
            Assert.That(small.MaxQueueMs(15_000, 60_000), Is.LessThan(800));
        });
    }

    [Test]
    public void AReceiversDownlinkThatArrivesAfterAStartCut_BoundsTheRecovery()
    {
        // The receiver's 1.5 Mbit/s downlink filled before its report arrived: the sender was cut first, then heard of it.
        var sim = new StartSimulation(capacityKbps: 20_000, oneWayMs: 15, preferredHeight: 1080);
        sim.Begin(new StartHints(UplinkKbps: 7_500, UplinkAtLeast: true));
        sim.ExtraDelay = (1_200, 2_200, 1_500);
        sim.Run(1_800);
        sim.ReceiversDownlink = (1_500, false);
        sim.Run(8_000);
        Assert.That(sim.MaxEstimate(2_500, 9_800), Is.LessThanOrEqualTo(1_500), "the recovery stops under the receiver's limit");
    }

    [Test]
    public void TwoStartTransients_AreBothRecoveredFrom()
    {
        // WebKit's page stalled twice while both sides started (2.0 s and 6.0 s in): each cut is a transient.
        var sim = new StartSimulation(capacityKbps: 20_000, oneWayMs: 15, preferredHeight: 1080);
        sim.Begin(new StartHints(UplinkKbps: 7_500, UplinkAtLeast: true));
        sim.ExtraDelay = (2_000, 3_000, 1_500);
        sim.SecondExtraDelay = (6_000, 7_000, 1_500);
        sim.Run(30_000);
        TestContext.Out.WriteLine(sim);
        Assert.Multiple(() =>
        {
            Assert.That(sim.Height, Is.EqualTo(1080));
            Assert.That(sim.LastRungChangeMs, Is.LessThanOrEqualTo(10_000), "back at the preference within the start window, not by the slow ramp");
        });
    }

    [Test]
    public void ANewPicturesOwnQueue_IsNotCongestion_ButTheRelaysReportsStillAre()
    {
        // WebKit drains a fresh data channel's first keyframe slowly: the pacer's queue stands while the relay sees none.
        var controller = new SendRateController(300);
        var start = new PictureStart(controller);
        start.Begin(0, new StartHints(UplinkKbps: 7_500, UplinkAtLeast: true), AudioWire, new VideoRateLadder(1), null);
        var begun = controller.EstimateKbps;
        for (long now = 250; now <= 2_500; now += 250)
            controller.Update(new SendPathSample(now, 800, 60, 600, 120, now == 750, Transport: new TransportSignal(now, 5, 0, 0, 800)));
        Assert.That(controller.EstimateKbps, Is.GreaterThanOrEqualTo(begun), "its own queue did not cut it");
        for (long now = 2_750; now <= 3_500; now += 250)
            controller.Update(new SendPathSample(now, 800, 60, 0, 0, false, new RelaySignal(now, 900, 0, 1_000, true, BaseLost: true)));
        Assert.That(controller.EstimateKbps, Is.LessThan(begun / 2), "the relay's report of a lost picture did");
    }

    // ── Helpers ──

    /// <summary>
    /// Runs the probe's judgement over a modelled link: each step's messages paced at its rate, queued through an uplink
    /// and (for the echo) a downlink bottleneck, reported the way the relay's transport feedback reports them.
    /// </summary>
    private static LinkProbeResult ProbeThrough(int upKbps, int downKbps, int lossEvery = 0, bool echo = true)
    {
        var probe = new LinkProbe();
        var steps = new List<ProbeStep>();
        long now = 0; // µs
        double upFree = 0, downFree = 0;
        ushort sequence = 0;
        var askEcho = echo;
        var echoFailures = 0;
        const int bytes = 1_103;
        foreach (var rate in LinkProbe.DefaultStepsKbps)
        {
            var step = probe.BeginStep(rate);
            var count = rate * LinkProbe.DefaultStepMs / 8 / bytes;
            var arrivals = new List<long>();
            var first = sequence;
            for (uint index = 0; index < count; index++)
            {
                var sent = now + (long)(index * bytes * 8_000.0 / rate);
                probe.OnSent(step, index, sequence, bytes, sent);
                var lost = lossEvery > 0 && sequence % lossEvery == 7;
                sequence++;
                upFree = Math.Max(upFree, sent) + bytes * 8_000.0 / upKbps;
                var atRelay = (long)upFree + 10_000;
                arrivals.Add(lost ? -1 : atRelay);
                if (lost || !askEcho) continue;
                downFree = Math.Max(downFree, atRelay) + bytes * 8_000.0 / downKbps;
                probe.OnEcho(step, index, (long)downFree + 10_000);
            }
            probe.OnFeedback(first, arrivals);
            now += LinkProbe.DefaultStepMs * 1_000 + 300_000;
            var verdict = probe.Judge(step, stamped: true, askEcho);
            steps.Add(verdict);
            if (!verdict.UplinkPassed) break;
            if (askEcho) echoFailures = verdict.EchoPassed ? 0 : echoFailures + 1;
            if (echoFailures >= 2) askEcho = false;
        }
        return LinkProbe.Conclude(steps, 0);
    }

    /// <summary>
    /// A sender (the controller's allocation, the ladder's picture) into a bottleneck drained at <see cref="CapacityKbps"/>,
    /// with a relay report every 250 ms one one-way delay later, keyframes on every size change, and the relay dropping
    /// video past 1.5 s of queue. <see cref="Begin"/> starts the picture through <see cref="PictureStart"/>.
    /// </summary>
    private sealed class StartSimulation
    {
        private readonly SendRateController _controller = new(300);
        private readonly VideoRateLadder _ladder = new(1);
        private readonly Queue<(long At, RelaySignal Signal)> _inFlight = new();
        private readonly int _oneWayMs;
        private double _queueBytes;
        private int _sentWindowBytes, _audioWindowBytes;
        private RelaySignal? _relay;
        private long _nextReport, _nextTick;
        private double _nextPicture;
        private bool _dropping, _keyframe = true;
        private readonly List<(long At, int Estimate, int QueueMs, VideoSetting Setting)> _trace = [];
        private readonly List<(long At, int Height)> _rungs = [];
        private int _grants;

        public StartSimulation(int capacityKbps, int oneWayMs, int preferredHeight)
        {
            CapacityKbps = capacityKbps;
            _oneWayMs = oneWayMs;
            _ladder.SetCeiling(preferredHeight, allow60: false);
            Start = new PictureStart(_controller);
        }

        public PictureStart Start { get; }
        public int CapacityKbps { get; set; }
        public (int Kbps, bool AtLeast)? ReceiversDownlink { get; set; }
        /// <summary>A receiver's page busy from..to: its reports carry this much more delay (and no capacity).</summary>
        public (long From, long To, int Ms)? ExtraDelay { get; set; }
        public (long From, long To, int Ms)? SecondExtraDelay { get; set; }
        public long LastRungChangeMs => _rungs[^1].At;
        public long Now { get; private set; }
        public int Estimate => _controller.EstimateKbps;
        public int Height => _ladder.Current.Rung.Height;
        public bool Suspended => _controller.VideoSuspended;
        public int FirstHeight => _rungs[0].Height;
        public int RungChanges => _rungs.Count - 1;
        public int RungChangesAfter(long at) => _rungs.Skip(1).Count(x => x.At >= at);
        public int LargestKeyframeBytes { get; private set; }
        public long? TimeToHeightMs(int height) => _rungs.FirstOrDefault(x => x.Height >= height) is { Height: > 0 } hit ? hit.At : null;
        public int MaxQueueMs(long from, long to) => _trace.Where(x => x.At >= from && x.At <= to).Select(x => x.QueueMs).DefaultIfEmpty().Max();
        public int EstimateAt(long at) => _trace.Last(x => x.At <= at).Estimate;
        public int MaxEstimate(long from, long to) => _trace.Where(x => x.At >= from && x.At <= to).Max(x => x.Estimate);

        public void Begin(StartHints hints)
        {
            var setting = Start.Begin(Now, hints, AudioWire, _ladder, ReceiversDownlink);
            _rungs.Add((Now, setting.Rung.Height));
        }

        public void Run(long milliseconds)
        {
            var end = Now + milliseconds;
            for (; Now < end; Now += 10)
            {
                if (Now % 20 == 0) Offer(80 + 130, audio: true);
                var setting = _ladder.Current;
                if (!_controller.VideoSuspended && Now >= _nextPicture)
                {
                    _nextPicture = Math.Max(_nextPicture + 1000.0 / setting.Rung.Framerate, Now);
                    var perPicture = setting.BitrateKbps * 1000 / 8.0 / setting.Rung.Framerate;
                    var factor = setting.Rung.Height >= 1080 ? 8 : setting.Rung.Height >= 720 ? 7 : 6;
                    var size = (int)(perPicture * (_keyframe ? factor : 0.9));
                    if (_keyframe) LargestKeyframeBytes = Math.Max(LargestKeyframeBytes, size);
                    _keyframe = false;
                    if (!_dropping) Offer(size, audio: false);
                }
                var drain = Math.Min(_queueBytes, CapacityKbps * 10 / 8.0);
                _queueBytes -= drain;
                var queueMs = (int)(_queueBytes * 8 / CapacityKbps);
                _dropping = queueMs > 1_500;
                if (_dropping) _queueBytes = Math.Min(_queueBytes, CapacityKbps * 1_000 / 8.0);
                if (Now >= _nextReport)
                {
                    _nextReport = Now + 250;
                    var busy = ExtraDelay is { } extra && Now >= extra.From && Now < extra.To ? extra.Ms
                        : SecondExtraDelay is { } again && Now >= again.From && Now < again.To ? again.Ms : 0;
                    _inFlight.Enqueue((Now + _oneWayMs, new RelaySignal(0, queueMs + busy, 0, queueMs >= 40 ? CapacityKbps : 0, _dropping, BaseLost: _dropping)));
                }
                while (_inFlight.Count > 0 && _inFlight.Peek().At <= Now) _relay = _inFlight.Dequeue().Signal with { ReceivedAtMs = Now };
                if (Now >= _nextTick)
                {
                    _nextTick = Now + SendRateLoop.IntervalMs;
                    var sent = _sentWindowBytes * 8 / SendRateLoop.IntervalMs;
                    var audio = _audioWindowBytes * 8 / SendRateLoop.IntervalMs;
                    _sentWindowBytes = _audioWindowBytes = 0;
                    Start.Tick(Now, ReceiversDownlink);
                    var decision = _controller.Update(new SendPathSample(Now, sent, audio, 0, 0, false, _relay));
                    // As SendRateLoop does: a start transient's steps down keep the ladder's fast climb.
                    if (_controller.RecoveryGrants != _grants) { _grants = _controller.RecoveryGrants; _ladder.TransientCut(Now); }
                    if (!decision.VideoSuspended && _ladder.Place(decision.VideoKbps, Now, decision.Signal != RateSignal.Normal) is { } placed &&
                        placed.Rung.Height != _rungs[^1].Height)
                    {
                        _rungs.Add((Now, placed.Rung.Height));
                        _keyframe = true;
                    }
                    _trace.Add((Now, decision.TotalKbps, queueMs, _ladder.Current));
                }
            }
        }

        private void Offer(int bytes, bool audio)
        {
            _queueBytes += bytes;
            // The link queues the full wire packet, while the production pacer samples media frames.
            // AudioWireKbps adds the 80 outer transport bytes to that sample when reserving audio.
            var sampledBytes = audio ? bytes - 80 : bytes;
            _sentWindowBytes += sampledBytes;
            if (audio) _audioWindowBytes += sampledBytes;
        }

        public override string ToString() => $"rungs: {string.Join(" ", _rungs.Select(x => $"{x.At / 1000.0:F2}s:{x.Height}p"))}\n" +
            string.Join("\n", _trace.Where((_, i) => i % 4 == 0).Select(x => $"{x.At / 1000.0,6:F1}s E={x.Estimate,5} q={x.QueueMs,5} {x.Setting}"));
    }
}
