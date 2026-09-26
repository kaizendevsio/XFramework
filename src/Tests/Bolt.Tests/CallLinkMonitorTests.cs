using Bolt.Media;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// The phone's view of its call transport, on a fake clock: when silence is evidence, how the
/// thresholds stretch with the round trip so a 1 s RTT link is slow rather than dead, and how
/// resume attempts are paced, cut short by a network hint, and bounded by the seat's grace period.
/// </summary>
public sealed class CallLinkMonitorTests
{
    [Test]
    public void Silence_IsNotEvidence_UntilTheRelayHasAnsweredAHeartbeat()
    {
        var link = new CallLinkMonitor();
        link.Connected(0);
        // A relay without heartbeats, or a call where nobody talks: a minute of nothing is not a dead link.
        Assert.That(link.Evaluate(60_000), Is.EqualTo(CallLinkState.Connected));
    }

    [Test]
    public void FastLink_IsPoorAfterThreeQuietSeconds_AndLostAfterEight()
    {
        var link = Answered(rtt: 50);
        Assert.Multiple(() =>
        {
            Assert.That(link.Evaluate(2_900), Is.EqualTo(CallLinkState.Connected));
            Assert.That(link.Evaluate(3_100), Is.EqualTo(CallLinkState.Degraded));
            Assert.That(link.Evaluate(7_900), Is.EqualTo(CallLinkState.Degraded));
            Assert.That(link.Evaluate(8_100), Is.EqualTo(CallLinkState.Reconnecting));
            Assert.That(link.LostAtMs, Is.EqualTo(8_100));
            Assert.That(link.Lost(9_000), Is.False, "the loss is reported once");
            Assert.That(link.LostAtMs, Is.EqualTo(8_100), "and dated from when it was noticed");
        });
    }

    [Test]
    public void OneSecondRttLink_WithHeartbeatsFlowing_IsNeverDead()
    {
        var link = new CallLinkMonitor();
        link.Connected(0);
        // A heartbeat every 2 s, each answered a full second later, for ten minutes.
        for (long sent = 0; sent < 600_000; sent += 2_000)
        {
            Assert.That(link.Evaluate(sent), Is.Not.EqualTo(CallLinkState.Reconnecting), $"t={sent}");
            link.Echo(sent, sent + 1_000);
        }
        Assert.Multiple(() =>
        {
            Assert.That(link.SmoothedRttMs, Is.EqualTo(1_000).Within(1));
            Assert.That(link.Evaluate(600_500), Is.EqualTo(CallLinkState.Connected), "one second is slow, not poor");
        });
    }

    [Test]
    public void Thresholds_StretchWithTheRoundTrip()
    {
        var slow = Answered(rtt: 2_000);
        Assert.Multiple(() =>
        {
            Assert.That(slow.DegradedAfterMs, Is.EqualTo(6_000), "3 × srtt");
            Assert.That(slow.LostAfterMs, Is.EqualTo(12_000), "6 × srtt");
            Assert.That(slow.Evaluate(11_000), Is.EqualTo(CallLinkState.Degraded), "eleven quiet seconds at 2 s RTT is still one lost ACK");
            Assert.That(slow.Evaluate(12_100), Is.EqualTo(CallLinkState.Reconnecting));
            Assert.That(slow.ResumeAttemptTimeoutMs, Is.EqualTo(16_000), "8 × srtt for a ticket, a socket and a rejoin");
            Assert.That(slow.EpochConfirmationTimeoutMs, Is.EqualTo(30_000));
            Assert.That(Answered(rtt: 4_000).EpochConfirmationTimeoutMs, Is.EqualTo(40_000), "10 × srtt once that exceeds 30 s");
        });
        var fast = Answered(rtt: 20);
        Assert.Multiple(() =>
        {
            Assert.That(fast.DegradedAfterMs, Is.EqualTo(3_000), "floors apply to fast links");
            Assert.That(fast.LostAfterMs, Is.EqualTo(8_000));
            Assert.That(fast.ResumeAttemptTimeoutMs, Is.EqualTo(10_000));
        });
    }

    [Test]
    public void SlowRoundTrip_ShowsPoorConnection_AndRecoversWithHysteresis()
    {
        var link = new CallLinkMonitor();
        link.Connected(0);
        long now = 0;
        void Heartbeat(long rtt) { link.Echo(now, now + rtt); now += 2_000; }
        for (var i = 0; i < 30; i++) Heartbeat(1_500);
        Assert.That(link.Evaluate(now), Is.EqualTo(CallLinkState.Degraded));
        for (var i = 0; i < 4; i++) Heartbeat(1_000);
        Assert.That(link.Evaluate(now), Is.EqualTo(CallLinkState.Degraded), "just under the limit is not yet good again");
        for (var i = 0; i < 30; i++) Heartbeat(100);
        Assert.That(link.Evaluate(now), Is.EqualTo(CallLinkState.Connected));
    }

    [Test]
    public void NetworkHint_GivesTheSocketAShortChanceToProveItSurvived()
    {
        var survived = Answered(rtt: 50);
        survived.Probe(1_000);
        survived.Inbound(1_400);
        Assert.That(survived.Evaluate(5_000), Is.Not.EqualTo(CallLinkState.Reconnecting), "an answer after the hint clears it");

        var dead = Answered(rtt: 50);
        dead.Probe(1_000);
        Assert.Multiple(() =>
        {
            Assert.That(dead.Evaluate(3_900), Is.Not.EqualTo(CallLinkState.Reconnecting));
            Assert.That(dead.Evaluate(4_000), Is.EqualTo(CallLinkState.Reconnecting), "no answer within 3 s of switching networks: the old socket is gone");
        });

        var slow = Answered(rtt: 1_500);
        slow.Probe(1_000);
        Assert.That(slow.Evaluate(5_000), Is.Not.EqualTo(CallLinkState.Reconnecting), "the probe window is 3 × srtt on a slow link");
    }

    [Test]
    public void Grace_CountsDownFromTheLoss_AndAResumeResetsTheEvidence()
    {
        var link = Answered(rtt: 100);
        Assert.That(link.Lost(10_000), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(link.RemainingGraceMs(10_000), Is.EqualTo(45_000));
            Assert.That(link.RemainingGraceMs(40_000), Is.EqualTo(15_000));
            Assert.That(link.RemainingGraceMs(99_000), Is.Zero);
        });
        link.Resumed(20_000);
        Assert.Multiple(() =>
        {
            Assert.That(link.State, Is.EqualTo(CallLinkState.Connected));
            Assert.That(link.LostAtMs, Is.Null);
            Assert.That(link.HeartbeatsAnswered, Is.False, "the new connection has to answer for itself");
            Assert.That(link.SmoothedRttMs, Is.EqualTo(100).Within(1), "but the round trip it will likely have is kept");
            Assert.That(link.Evaluate(90_000), Is.EqualTo(CallLinkState.Connected));
        });
    }

    [TestCase(0, 0)]
    [TestCase(1, 500)]
    [TestCase(2, 1_000)]
    [TestCase(3, 2_000)]
    [TestCase(4, 3_000)]
    [TestCase(5, 5_000)]
    [TestCase(9, 5_000)]
    public void Backoff_FollowsTheSchedule(int attempt, int expected)
    {
        var link = new CallLinkMonitor();
        Assert.Multiple(() =>
        {
            Assert.That(link.BackoffDelayMs(attempt, 0.5), Is.EqualTo(expected), "no jitter at the midpoint");
            Assert.That(link.BackoffDelayMs(attempt, 0), Is.EqualTo((int)Math.Round(expected * 0.8)), "-20%");
            Assert.That(link.BackoffDelayMs(attempt, 0.999999), Is.EqualTo(expected * 1.2).Within(1), "+20%");
        });
    }

    // ── The reconnect loop ──

    [Test]
    public async Task Reconnect_TriesAtOnce_ThenBacksOff_UntilItResumes()
    {
        var (link, clock, reconnector) = Reconnecting();
        var starts = new List<long>();
        var outcome = reconnector.RunAsync(_ =>
        {
            starts.Add(clock.Now);
            return Task.FromResult(starts.Count < 5 ? CallResumeAttempt.Retry : CallResumeAttempt.Resumed);
        }, CancellationToken.None);
        await clock.RunUntilAsync(outcome);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(CallResumeOutcome.Resumed));
            Assert.That(starts, Is.EqualTo(new long[] { 0, 500, 1_500, 3_500, 6_500 }), "immediately, then 0.5, 1, 2, 3 s apart");
            Assert.That(reconnector.Attempts, Is.EqualTo(5));
            Assert.That(link.State, Is.EqualTo(CallLinkState.Connected));
        });
    }

    [Test]
    public async Task Reconnect_StopsAtOnce_WhenTheServerRefuses()
    {
        var (link, clock, reconnector) = Reconnecting();
        var calls = 0;
        var outcome = reconnector.RunAsync(_ => Task.FromResult(++calls == 2 ? CallResumeAttempt.Refused : CallResumeAttempt.Retry), CancellationToken.None);
        await clock.RunUntilAsync(outcome);
        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(CallResumeOutcome.Refused));
            Assert.That(calls, Is.EqualTo(2), "a refused seat is not retried");
            Assert.That(link.State, Is.EqualTo(CallLinkState.Failed));
        });
    }

    [Test]
    public async Task Reconnect_GivesUpWhenTheGraceRunsOut()
    {
        var (link, clock, reconnector) = Reconnecting();
        var starts = new List<long>();
        var outcome = reconnector.RunAsync(_ => { starts.Add(clock.Now); return Task.FromResult(CallResumeAttempt.Retry); }, CancellationToken.None);
        await clock.RunUntilAsync(outcome);
        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(CallResumeOutcome.GaveUp));
            Assert.That(clock.Now, Is.EqualTo(45_000), "exactly at the end of the seat hold, not a backoff step later");
            Assert.That(starts.Last(), Is.LessThan(45_000));
            Assert.That(starts.Skip(6).Zip(starts.Skip(7), (a, b) => b - a), Is.All.EqualTo(5_000), "every 5 s once the schedule tops out");
            Assert.That(link.State, Is.EqualTo(CallLinkState.Failed));
        });
    }

    [Test]
    public async Task Reconnect_NetworkHintCutsTheWaitShort()
    {
        var (_, clock, reconnector) = Reconnecting();
        var starts = new List<long>();
        var outcome = reconnector.RunAsync(_ =>
        {
            starts.Add(clock.Now);
            return Task.FromResult(starts.Count < 4 ? CallResumeAttempt.Retry : CallResumeAttempt.Resumed);
        }, CancellationToken.None);
        await clock.UntilAsync(() => starts.Count == 3 && clock.Pending == 1); // waiting 2 s before the fourth attempt
        clock.Advance(700);
        reconnector.Nudge();
        await clock.RunUntilAsync(outcome);
        Assert.That(starts, Is.EqualTo(new long[] { 0, 500, 1_500, 2_200 }), "the fourth attempt starts on the hint, not 2 s later");
    }

    [Test]
    public async Task Reconnect_AbandonsAnAttemptThatHangs()
    {
        var (_, clock, reconnector) = Reconnecting();
        var tokens = new List<CancellationToken>();
        var outcome = reconnector.RunAsync(async ct =>
        {
            tokens.Add(ct);
            if (tokens.Count == 1) { await Task.Delay(Timeout.Infinite, ct); }
            return CallResumeAttempt.Resumed;
        }, CancellationToken.None);
        await clock.RunUntilAsync(outcome);
        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(CallResumeOutcome.Resumed));
            Assert.That(tokens, Has.Count.EqualTo(2));
            Assert.That(tokens[0].IsCancellationRequested, Is.True, "the hung attempt is told to stop");
            Assert.That(clock.Now, Is.EqualTo(10_500), "after its 10 s timeout and the 0.5 s backoff");
        });
    }

    [Test]
    public async Task Reconnect_EndsQuietlyWhenTheCallIsHungUp()
    {
        var (_, clock, reconnector) = Reconnecting();
        using var hangup = new CancellationTokenSource();
        var outcome = reconnector.RunAsync(_ => Task.FromResult(CallResumeAttempt.Retry), hangup.Token);
        await clock.UntilAsync(() => reconnector.Attempts == 2 && clock.Pending == 1);
        hangup.Cancel();
        Assert.That(await outcome.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(CallResumeOutcome.Cancelled));
    }

    /// <summary>
    /// One view of the link: the send rate controller's verdict (a standing queue above its high-delay threshold,
    /// or video suspended for bandwidth) shows as "Poor connection" once it holds, clears only once it has been gone
    /// as long, and never makes a link that keeps delivering "Reconnecting".
    /// </summary>
    [Test]
    public void ACongestedSendPath_IsPoorConnection_WithHysteresis_AndNeverReconnecting()
    {
        var link = Answered(rtt: 100);
        long now = 0;
        void Tick(bool poor)
        {
            now += 250;
            link.Inbound(now);
            if (now % 2_000 == 0) link.Echo(now - 100, now);
            link.SendPath(poor, now);
            Assert.That(link.Evaluate(now), Is.Not.EqualTo(CallLinkState.Reconnecting), $"t={now}");
        }

        for (var i = 0; i < 8; i++) Tick(poor: true);
        Assert.That(link.State, Is.EqualTo(CallLinkState.Connected), "a two-second spike the controller handles is not shown");
        for (var i = 0; i < 8; i++) Tick(poor: true);
        Assert.That(link.State, Is.EqualTo(CallLinkState.Degraded), "a queue the controller keeps fighting is");
        for (var i = 0; i < 240; i++) Tick(poor: true);
        Assert.That(link.State, Is.EqualTo(CallLinkState.Degraded), "a minute of congestion stays a poor connection, not a dead one");
        for (var i = 0; i < 8; i++) Tick(poor: false);
        Assert.That(link.State, Is.EqualTo(CallLinkState.Degraded), "the notice does not flap off on a good moment");
        Tick(poor: true);
        for (var i = 0; i < 13; i++) Tick(poor: false);
        Assert.That(link.State, Is.EqualTo(CallLinkState.Connected), "it clears once the path has been fine for the hold");

        link.SendPath(true, now);
        link.Connected(now + 1);
        Assert.That(link.SendPathPoor, Is.False, "a new transport starts without the old path's verdict");
    }

    private static CallLinkMonitor Answered(double rtt)
    {
        var link = new CallLinkMonitor();
        link.Connected(0);
        // Enough echoes that the smoothed round trip has converged, all ending at t = 0.
        for (var i = 0; i < 80; i++) link.Echo(-(long)rtt, 0);
        return link;
    }

    private static (CallLinkMonitor, ManualClock, CallReconnector) Reconnecting()
    {
        var clock = new ManualClock();
        var link = Answered(rtt: 50);
        link.Lost(0);
        return (link, clock, new CallReconnector(link, () => clock.Now, clock.Delay, () => 0.5));
    }

    /// <summary>A clock that only moves when the test (or <see cref="RunUntilAsync"/>) moves it.</summary>
    private sealed class ManualClock
    {
        private readonly List<(long Due, TaskCompletionSource Done)> waits = [];
        private long now;
        public long Now { get { lock (waits) return now; } }
        public int Pending { get { lock (waits) return waits.Count(x => !x.Done.Task.IsCompleted); } }

        public Task Delay(TimeSpan span, CancellationToken ct)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (waits) waits.Add((now + (long)span.TotalMilliseconds, done));
            ct.Register(() => done.TrySetCanceled(ct));
            return done.Task;
        }

        public void Advance(long ms)
        {
            lock (waits) { now += ms; Release(); }
        }

        /// <summary>Jump to the next pending deadline whenever everything is waiting on the clock, until <paramref name="task"/> ends.</summary>
        public async Task RunUntilAsync(Task task)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline) Assert.Fail("the reconnect loop did not finish");
                await Task.Delay(5);
                lock (waits)
                {
                    var next = waits.Where(x => !x.Done.Task.IsCompleted).Select(x => (long?)x.Due).Min();
                    if (next is { } due) { now = Math.Max(now, due); Release(); }
                }
            }
            await task;
        }

        /// <summary>Like <see cref="RunUntilAsync"/>, but stops as soon as <paramref name="condition"/> holds.</summary>
        public async Task UntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (true)
            {
                if (DateTime.UtcNow > deadline) Assert.Fail("condition not reached");
                await Task.Delay(5);
                if (condition()) return;
                lock (waits)
                {
                    var next = waits.Where(x => !x.Done.Task.IsCompleted).Select(x => (long?)x.Due).Min();
                    if (next is { } due) { now = Math.Max(now, due); Release(); }
                }
            }
        }

        private void Release()
        {
            foreach (var (due, done) in waits.Where(x => x.Due <= now).ToArray()) done.TrySetResult();
            waits.RemoveAll(x => x.Done.Task.IsCompleted);
        }
    }
}
