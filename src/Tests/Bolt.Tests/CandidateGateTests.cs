using Bolt.Protocol.Transport;
using NUnit.Framework;

namespace Bolt.Tests;

public sealed class CandidateGateTests
{
    [Test]
    public async Task ReleaseAsync_EndOfGatheringDuringSend_StaysBehindHeldCandidates()
    {
        var gate = new CandidateGate();
        var first = new RtcCandidate("first", "0", 0);
        var second = new RtcCandidate("second", "0", 0);
        var end = new RtcCandidate("", null, null);
        gate.Hold();
        gate.TryHold(first);
        gate.TryHold(second);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unblock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sent = new List<RtcCandidate>();
        var draining = gate.ReleaseAsync(async candidate =>
        {
            if (candidate == first) { entered.SetResult(); await unblock.Task; }
            sent.Add(candidate);
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var held = gate.TryHold(end);
        unblock.SetResult();
        await draining.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Multiple(() =>
        {
            Assert.That(held, Is.True, "the completion marker must not bypass a drain in progress");
            Assert.That(sent, Is.EqualTo(new[] { first, second, end }));
            Assert.That(gate.TryHold(first), Is.False, "normal trickling resumes after the queue drains");
        });
    }
}
