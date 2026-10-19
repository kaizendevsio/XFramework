using Bolt.Media.Congestion;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public class MediaPacerRecoveryTests
{
    [Test]
    public async Task StalePartialPicture_ExpiresWithDependents_AndRecoversOnFreshKeyframe()
    {
        long now = 0;
        await using var pacer = new MediaSendPacer((_, _) => ValueTask.CompletedTask, clock: () => now);
        var requests = 0;
        pacer.KeyframeNeeded += () => requests++;
        pacer.EnqueueVideo(new([[1], [2], [3]], true));
        Assert.That(pacer.TryTake(out _, out _), Is.True);
        now = 100;
        pacer.EnqueueVideo(new([[4]], false));
        now = 501;
        Assert.That(pacer.TryTake(out _, out _), Is.False, "unfinished old picture and its dependents are obsolete");
        Assert.That(requests, Is.EqualTo(1));
        Assert.That(pacer.Sample().BaseLosses, Is.EqualTo(1));
        Assert.That(pacer.WouldAccept(false, 0), Is.False);
        pacer.EnqueueAudio([9]);
        Assert.That(pacer.TryTake(out var audio, out var isAudio), Is.True);
        Assert.That(isAudio, Is.True);
        Assert.That(audio, Is.EqualTo(new byte[] { 9 }));
        pacer.EnqueueVideo(new([[5]], true));
        Assert.That(pacer.TryTake(out var fresh, out _), Is.True);
        Assert.That(fresh, Is.EqualTo(new byte[] { 5 }));
    }

    [Test]
    public async Task Backpressure_ExpiresVideoWithoutNeedingMoreEncoderOutput()
    {
        long now = 0;
        await using var pacer = new MediaSendPacer((_, _) => ValueTask.CompletedTask, () => 100_000, clock: () => Interlocked.Read(ref now));
        pacer.EnqueueVideo(new([[1]], true));
        pacer.Start();
        Interlocked.Exchange(ref now, 501);
        Assert.That(() => pacer.IsAwaitingKeyframe, Is.True.After(1000, 10));
        Assert.That(pacer.Sample().DroppedPictures, Is.EqualTo(1));
    }

    [Test]
    public async Task LowRate_LargeSocketFragment_CanLeaveTheTokenBucket()
    {
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var pacer = new MediaSendPacer((_, _) => { sent.TrySetResult(); return ValueTask.CompletedTask; });
        pacer.RateKbps = 16;
        pacer.EnqueueVideo(new([new byte[16 * 1024]], true));
        pacer.Start();
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task FailedFragment_DiscardsDependentVideoAndRequestsRecovery()
    {
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        await using var pacer = new MediaSendPacer((_, _) =>
        {
            Interlocked.Increment(ref attempts);
            throw new IOException("transport disconnected");
        });
        pacer.KeyframeNeeded += () => requested.TrySetResult();
        pacer.EnqueueVideo(new([[1], [2]], true));
        pacer.EnqueueVideo(new([[3]], false));
        pacer.Start();
        await requested.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(pacer.IsAwaitingKeyframe, Is.True);
        Assert.That(attempts, Is.EqualTo(1));
        Assert.That(pacer.Sample().BaseLosses, Is.EqualTo(1));
    }

    [Test]
    public async Task PathChange_ClearsOldFragmentsRepairsAndSamples_AndRequestsKeyframe()
    {
        long now = 0;
        await using var pacer = new MediaSendPacer((_, _) => ValueTask.CompletedTask, clock: () => now);
        var requests = 0; pacer.KeyframeNeeded += () => requests++;
        pacer.EnqueueVideo(new([[1], [2]], true));
        pacer.TryTake(out _, out _);
        pacer.EnqueueVideo(new([[3]], false));
        pacer.EnqueueAudio([4]);
        pacer.EnqueueVideoRepair([5], 1000, (_, _) => ValueTask.CompletedTask);
        now = 100;
        pacer.ResetForPathChange();
        Assert.That(pacer.TryTake(out _, out _), Is.False);
        Assert.That(pacer.WouldAccept(false, 0), Is.False);
        Assert.That(requests, Is.EqualTo(1));
        Assert.That(pacer.Sample().QueueDelayMs, Is.Zero);
        Assert.That(pacer.Sample().BaseLosses, Is.Zero, "a new path must not inherit the old path's congestion");
        pacer.EnqueueVideo(new([[6]], true));
        Assert.That(pacer.TryTake(out var fresh, out _), Is.True);
        Assert.That(fresh, Is.EqualTo(new byte[] { 6 }));
    }
}
