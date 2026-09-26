using Bolt.Media.Congestion;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// Where phase 1's rate control meets phase 2's resumable calls: after an outage the path starts over from a safe
/// estimate (half the old path's stable rate, never below the call's start tier), with none of the old path's delay
/// history, congestion point or video suspension, whether it was this sender's transport that was replaced or its
/// only receiver that went away and came back.
/// </summary>
[CancelAfter(20_000)]
public sealed class SendRateResumeTests
{
    private const int Audio = 84;

    [Test]
    public void StableRate_IsTheCalmOperatingPoint_NotAProbePeak_NorTheCutAnOutageCaused()
    {
        var controller = new SendRateController(600);
        long now = 0;
        // Calm at about 1.5 Mbps: the encoder sends 1 Mbps, so the estimate climbs to its ceiling (sent x 1.5 + 64).
        now = Calm(controller, now, 30_000, sentKbps: 1_000);
        var calm = controller.EstimateKbps;
        Assert.That(calm, Is.InRange(1_500, 1_600));
        Assert.That(controller.StableKbps, Is.InRange(calm * 9 / 10, calm), "it follows a rate the path held");

        // The link dies: the sender's own queue grows for 8 s before the transport is declared lost.
        for (var end = now + 8_000; now < end; now += 250)
            controller.Update(new SendPathSample(now, 200, Audio, (int)Math.Min(8_000, (now - end + 8_000) / 2 + 500), 0, false));
        Assert.Multiple(() =>
        {
            Assert.That(controller.EstimateKbps, Is.LessThan(calm / 2), "the controller cut hard during the outage");
            Assert.That(controller.StableKbps, Is.InRange(calm * 9 / 10, calm), "the cut is not what the path was stable at");
        });
    }

    [Test]
    public void AResumedTransport_StartsAtHalfTheStableRate_WithFreshState_AndClimbsBackQuickly()
    {
        var previous = new SendRateController(600, new SendRateOptions { RestartFloorKbps = 264 });
        var now = Calm(previous, 0, 30_000, sentKbps: 1_000);
        var stable = previous.StableKbps;
        // A hard outage on the old path: suspended video, a congestion point far below the stable rate.
        for (var end = now + 10_000; now < end; now += 250)
            previous.Update(new SendPathSample(now, 100, Audio, 6_000, 0, true, new RelaySignal(now, 6_000, 0, 60, true, true)));
        Assert.That(previous.VideoSuspended, Is.True);

        var resumed = SendRateController.Resume(previous, Audio);
        Assert.Multiple(() =>
        {
            Assert.That(resumed.EstimateKbps, Is.EqualTo(stable / 2).Within(2), "half the old stable rate, not its peak nor its cut");
            Assert.That(resumed.VideoSuspended, Is.False, "the new path gets to try video again");
            Assert.That(resumed.Options.RestartFloorKbps, Is.EqualTo(264), "the call's tuning carries over");
        });

        // Fresh reports on the new path are calm: before any congestion there, it climbs at the startup rate.
        var start = resumed.EstimateKbps;
        Calm(resumed, now, 6_000, sentKbps: 2_000);
        Assert.That(resumed.EstimateKbps, Is.GreaterThan(start * 3 / 2), "no congestion point from the old path slows it");
    }

    [Test]
    public void AResumeFromALowStableRate_NeverStartsBelowTheFloor()
    {
        var previous = new SendRateController(200, new SendRateOptions { RestartFloorKbps = 264 });
        Calm(previous, 0, 10_000, sentKbps: 100);
        Assert.That(SendRateController.Resume(previous, Audio).EstimateKbps, Is.EqualTo(264), "a voice-only call keeps its normal Opus rate");
        Assert.That(SendRateController.RestartKbps(10_000, new SendRateOptions(), Audio), Is.EqualTo(5_000));
        Assert.That(SendRateController.RestartKbps(0, new SendRateOptions(), Audio), Is.EqualTo(110 + Audio + 16),
            "without a configured floor, the point video resumes at");
    }

    [Test]
    public void TheOnlyReceiverComingBackFromALongSilence_RestartsThePath_AndBringsVideoBack()
    {
        var controller = new SendRateController(600, new SendRateOptions { RestartFloorKbps = 264 });
        var now = Calm(controller, 0, 30_000, sentKbps: 1_000, receiver: true);
        var stable = controller.StableKbps;
        // The receiver's link dies: the relay's queue towards it grows, it stops reporting, video is suspended...
        var lastHeard = now;
        for (var end = now + 10_000; now < end; now += 250)
            controller.Update(new SendPathSample(now, 120, Audio, 0, 0, false,
                new RelaySignal(now, (int)(now - lastHeard), 0, 40, true, now - lastHeard > 3_000)));
        Assert.That(controller.VideoSuspended, Is.True);
        // ...then the relay drops it, and the sender hears nothing downstream for a while.
        for (var end = now + 8_000; now < end; now += 250)
            controller.Update(new SendPathSample(now, 90, Audio, 0, 0, false));

        // It resumes on a new connection; its first delay report is calm.
        controller.Update(new SendPathSample(now, 90, Audio, 0, 0, false, new RelaySignal(now, 5, 0, 0, false), new ReceiverSignal(now, 5, 90)));
        Assert.Multiple(() =>
        {
            Assert.That(controller.EstimateKbps, Is.EqualTo(stable / 2).Within(2), "the path starts over from a safe estimate");
            Assert.That(controller.VideoSuspended, Is.False, "and video comes back with it, not after a backed-off resume hold");
            Assert.That(controller.Restarts, Is.EqualTo(1), "the loop restarts the picture ladder on this");
        });
    }

    [Test]
    public void ASilenceThatEndsWithAQueue_IsCongestion_NotAnOutage()
    {
        var controller = new SendRateController(600);
        var now = Calm(controller, 0, 30_000, sentKbps: 1_000, receiver: true);
        var lastHeard = now;
        now += 6_000;
        // A long TCP stall: the reports that finally arrive say the queue is still there.
        var decision = controller.Update(new SendPathSample(now, 1_000, Audio, 0, 0, false,
            new RelaySignal(now, 900, 0, 300, true), new ReceiverSignal(now, 900, 300)));
        Assert.That(decision.Signal, Is.EqualTo(RateSignal.Overuse));
        Assert.That(controller.EstimateKbps, Is.LessThan(1_000), "no restart: the path is congested, and is cut");
        Assert.That(controller.Restarts, Is.Zero);
        _ = lastHeard;
    }

    [Test]
    public void Ladder_RestartPlacesThePictureTheBudgetFits_WithoutTheOldPathsUpBackoff()
    {
        var ladder = new VideoRateLadder(VideoRateLadder.IndexForHeight(1080));
        // The old path failed its steps up: repeated drops right after going up back the ladder off.
        long now = 0;
        for (var i = 0; i < 4; i++)
        {
            for (var end = now + 20_000; now < end; now += 250) ladder.Place(6_000, now, false);
            ladder.Place(300, now, true);
        }
        var placed = ladder.Restart(700);
        Assert.That(placed.Rung.Height, Is.EqualTo(540), "the largest picture 700 kbps fits");
        Assert.That(placed.BitrateKbps, Is.EqualTo(700));
        for (var end = now + VideoRateLadder.UpHoldMs + 500; now < end; now += 250) ladder.Place(1_400, now, false);
        Assert.That(ladder.Current.Rung.Height, Is.EqualTo(720), "a step up after the usual hold, with no backoff inherited");
    }

    /// <summary>Calm windows: the sender sends <paramref name="sentKbps"/>, and the relay (and a receiver) report a short queue.</summary>
    private static long Calm(SendRateController controller, long now, long forMs, int sentKbps, bool receiver = false)
    {
        for (var end = now + forMs; now < end; now += 250)
            controller.Update(new SendPathSample(now, sentKbps, Audio, 5, 0, false, new RelaySignal(now, 10, 0, 0, false),
                receiver ? new ReceiverSignal(now, 10, sentKbps) : null));
        return now;
    }
}
