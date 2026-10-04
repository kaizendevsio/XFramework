using Bolt.Media.Browser;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// Selective retransmission for video on a datagram path, receiver side: what is asked for, when, what waits for it,
/// and what a loss that cannot be repaired in time costs (an enhancement picture's dependents, or a keyframe).
/// </summary>
public sealed class VideoRecoveryBufferTests
{
    /// <summary>A sender's pictures as fragments with their MediaFrame sequence numbers, as BoltMediaStream numbers them.</summary>
    private sealed class Sender
    {
        private uint _sequence = 1000;
        public List<(uint Sequence, byte[] Fragment, uint Frame)> Picture(uint frame, int layer, bool key = false, int fragments = 1)
        {
            var parts = VideoFrameFragments.Split(new byte[fragments * 300 - 100], frame, frame * 33_000, key, layer, payload: 300);
            Assert.That(parts, Has.Count.EqualTo(fragments));
            return parts.Select(part => (_sequence++, part, frame)).ToList();
        }
    }

    private static VideoRecoveryBuffer Recovering(int rttMs = 200)
    {
        var buffer = new VideoRecoveryBuffer();
        buffer.Configure(recover: true, rttMs);
        return buffer;
    }

    private static List<VideoFramePayload> Push(VideoRecoveryBuffer buffer, IEnumerable<(uint Sequence, byte[] Fragment, uint Frame)> fragments, long now = 0)
    {
        var ready = new List<VideoFramePayload>();
        foreach (var (sequence, fragment, _) in fragments) buffer.Push(sequence, fragment, now, ready);
        return ready;
    }

    private static (List<VideoFramePayload> Ready, List<uint> Nacks) Poll(VideoRecoveryBuffer buffer, long now)
    {
        var ready = new List<VideoFramePayload>();
        var nacks = new List<uint>();
        buffer.Poll(now, ready, nacks);
        return (ready, nacks);
    }

    [Test]
    public void ALostFragment_IsAskedFor_AndTheNextPictureWaitsForIt_ThenBothPlayInOrder()
    {
        var sender = new Sender();
        var buffer = Recovering();
        var key = sender.Picture(1, 0, key: true);
        var second = sender.Picture(2, 0, fragments: 3);
        var third = sender.Picture(3, 0);
        var lost = second[1];

        var ready = Push(buffer, key.Concat([second[0], second[2]]).Concat(third), now: 0);
        Assert.That(ready.Select(x => x.FrameId), Is.EqualTo(new[] { 1u }), "picture 3 may refer to 2: it waits");

        Assert.That(Poll(buffer, 5).Nacks, Is.Empty, "a reordered frame gets a moment first");
        Assert.That(Poll(buffer, 20).Nacks, Is.EqualTo(new[] { lost.Sequence }));

        var after = Push(buffer, [lost], now: 120);
        Assert.Multiple(() =>
        {
            Assert.That(after.Select(x => x.FrameId), Is.EqualTo(new[] { 2u, 3u }));
            Assert.That(after.Any(x => x.Discontinuity), Is.False, "repaired: no decoder reset, no keyframe");
            Assert.That(buffer.Recovered, Is.EqualTo(1));
        });
    }

    [Test]
    public void AFragmentTheSendersUplinkLost_IsStillWaitedFor_WhenItsSenderSendsItAgain()
    {
        // Production 2026-10-04 13:08 UTC, 13-14 ms round trips: a fragment lost between the sender and the relay can only
        // come from the sender, after the relay's next transport feedback report (every 100 ms) and two more legs. With
        // a window of 1.5 round trips + 50 ms (100 ms there) it always came too late: every large keyframe lost one, and
        // nothing was shown.
        var sender = new Sender();
        var buffer = Recovering(rttMs: 14);
        var key = sender.Picture(1, 0, key: true, fragments: 30);
        var lost = key[17];
        var ready = Push(buffer, key.Where(x => x.Sequence != lost.Sequence), now: 0);
        Assert.That(ready, Is.Empty);
        for (long t = 10; t <= 170; t += 10) ready.AddRange(Poll(buffer, t).Ready);
        ready.AddRange(Push(buffer, [lost], now: 175));
        Assert.Multiple(() =>
        {
            Assert.That(ready.Select(x => x.FrameId), Is.EqualTo(new[] { 1u }), "the keyframe is shown");
            Assert.That(buffer.Incomplete, Is.Zero);
        });
    }

    [Test]
    public void OnAWebSocket_NothingIsAskedFor_AndAGapIsTheAssemblersUsualBreak()
    {
        var sender = new Sender();
        var buffer = new VideoRecoveryBuffer();
        Assert.That(buffer.RecoveryMs, Is.Zero);
        var ready = Push(buffer, sender.Picture(1, 0, key: true));
        sender.Picture(2, 0); // relay dropped it
        ready.AddRange(Push(buffer, sender.Picture(3, 0)));
        Assert.Multiple(() =>
        {
            Assert.That(Poll(buffer, 1_000).Nacks, Is.Empty, "TCP retransmits; a gap there is a deliberate drop");
            Assert.That(ready.Select(x => x.FrameId), Is.EqualTo(new[] { 1u, 3u }));
            Assert.That(ready[1].Discontinuity, Is.True);
        });
    }

    [Test]
    public void NacksRepeatAfterARoundTrip_AndStopWhenAnAnswerCouldNoLongerArriveInTime()
    {
        var sender = new Sender();
        var buffer = Recovering(rttMs: 200); // window: 1.5 x 200 + 50, plus 150 for a repair from the sender = 500 ms
        Assert.That(buffer.RecoveryMs, Is.EqualTo(500));
        Push(buffer, sender.Picture(1, 0, key: true));
        var lost = sender.Picture(2, 0)[0];
        Push(buffer, sender.Picture(3, 0));

        var asked = new List<long>();
        for (long t = 0; t < 550; t += 10)
            if (Poll(buffer, t).Nacks.Contains(lost.Sequence)) asked.Add(t);

        Assert.Multiple(() =>
        {
            Assert.That(asked, Has.Count.EqualTo(2), "a third ask would arrive after the window closes");
            Assert.That(asked[0], Is.InRange(VideoRecoveryBuffer.ReorderMs, 30));
            Assert.That(buffer.Abandoned, Is.EqualTo(1));
        });

        // A short path (20 ms, the 250 ms minimum window) has time to ask again a round trip later.
        var shorter = Recovering(rttMs: 20);
        var other = new Sender();
        Push(shorter, other.Picture(1, 0, key: true));
        var missing = other.Picture(2, 0)[0];
        Push(shorter, other.Picture(3, 0));
        var times = new List<long>();
        for (long t = 0; t < 300; t += 2)
            if (Poll(shorter, t).Nacks.Contains(missing.Sequence)) times.Add(t);
        Assert.That(times, Has.Count.EqualTo(VideoRecoveryBuffer.MaxTries));
        Assert.That(times[1] - times[0], Is.GreaterThanOrEqualTo(20 * 6 / 5 + 10));
    }

    [Test]
    public void AnUnrepairableEnhancementPicture_CostsOnlyThePicturesThatReferToIt_NotAKeyframe()
    {
        // L1T3: T0 T2 T1 T2 T0. The T1 picture (3) loses a fragment for good; picture 4 (T2) refers to it.
        var sender = new Sender();
        var buffer = Recovering();
        var pictures = new[]
        {
            sender.Picture(1, 0, key: true), sender.Picture(2, 2), sender.Picture(3, 1, fragments: 2), sender.Picture(4, 2), sender.Picture(5, 0),
        };
        var ready = Push(buffer, pictures[0].Concat(pictures[1]).Concat([pictures[2][0]]).Concat(pictures[3]).Concat(pictures[4]));
        for (long t = 0; t <= buffer.RecoveryMs + 10; t += 10) ready.AddRange(Poll(buffer, t).Ready);

        Assert.Multiple(() =>
        {
            Assert.That(ready.Select(x => x.FrameId), Is.EqualTo(new[] { 1u, 2u, 5u }));
            Assert.That(ready.Any(x => x.Discontinuity), Is.False, "the base layer is intact: no decoder reset, no keyframe");
            Assert.That(buffer.Skipped, Is.EqualTo(1), "picture 4 referred to the lost T1");
        });
    }

    [Test]
    public void AnUnrepairableBasePicture_BreaksTheStream_SoTheDecoderAsksForAKeyframe()
    {
        var sender = new Sender();
        var buffer = Recovering();
        var key = sender.Picture(1, 0, key: true);
        var base2 = sender.Picture(2, 0, fragments: 2);
        var after = sender.Picture(3, 0);
        var ready = Push(buffer, key.Concat([base2[0]]).Concat(after));
        for (long t = 0; t <= buffer.RecoveryMs + 10; t += 10) ready.AddRange(Poll(buffer, t).Ready);
        Assert.That(ready.Select(x => x.FrameId), Is.EqualTo(new[] { 1u, 3u }));
        Assert.That(ready[1].Discontinuity, Is.True);
    }

    [Test]
    public void NothingWaitsForATopLayerPicture_WhichNoPictureRefersTo()
    {
        var sender = new Sender();
        var buffer = Recovering();
        var pictures = new[] { sender.Picture(1, 0, key: true), sender.Picture(2, 2, fragments: 2), sender.Picture(3, 1), sender.Picture(4, 2) };
        // Picture 4 marks layer 2 as the top before picture 2's loss is seen.
        var ready = Push(buffer, pictures[0].Concat(pictures[3].Take(0)).Concat([pictures[1][0]]).Concat(pictures[2]).Concat(pictures[3]));
        Assert.Multiple(() =>
        {
            Assert.That(ready.Select(x => x.FrameId), Is.EqualTo(new[] { 1u, 3u, 4u }), "the lost T2 holds nothing up");
            Assert.That(ready.Any(x => x.Discontinuity), Is.False);
        });
    }

    [Test]
    public void FramesTheRelayDeclined_AreNotWaitedFor_AndOnALayeredStreamAreNoBreak()
    {
        // The relay shed picture 2 (T2) for this receiver; the gap in sequence numbers looks like loss until it says so.
        var sender = new Sender();
        var buffer = Recovering();
        var ready = Push(buffer, sender.Picture(1, 0, key: true).Concat(sender.Picture(2, 1)));
        var shed = sender.Picture(3, 2, fragments: 2);
        var next = sender.Picture(4, 1);
        ready.AddRange(Push(buffer, next));
        Assert.That(ready.Select(x => x.FrameId), Is.EqualTo(new[] { 1u, 2u }), "picture 4 waits: the gap may have been its reference");

        var declined = new List<VideoFramePayload>();
        buffer.Decline(shed.Select(x => x.Sequence).ToArray(), declined);
        Assert.Multiple(() =>
        {
            Assert.That(declined.Select(x => x.FrameId), Is.EqualTo(new[] { 4u }));
            Assert.That(declined[0].Discontinuity, Is.False, "a policy drop on a layered stream needs no keyframe");
            Assert.That(buffer.Declined, Is.EqualTo(2));
        });
    }

    [Test]
    public void AWholePictureLostInTransit_IsABreakWhenItCannotBeRepaired()
    {
        // Nothing of picture 2 arrived, so its layer is unknown: it may have been a base picture.
        var sender = new Sender();
        var buffer = Recovering();
        var ready = Push(buffer, sender.Picture(1, 0, key: true).Concat(sender.Picture(2, 1)));
        sender.Picture(3, 0);
        ready.AddRange(Push(buffer, sender.Picture(4, 2)));
        for (long t = 0; t <= buffer.RecoveryMs + 10; t += 10) ready.AddRange(Poll(buffer, t).Ready);
        Assert.That(ready.Select(x => x.FrameId), Is.EqualTo(new[] { 1u, 2u, 4u }));
        Assert.That(ready[2].Discontinuity, Is.True);
    }

    [Test]
    public void LateAndReplayedFragments_OfAPictureAlreadyHandled_AreIgnored()
    {
        var sender = new Sender();
        var buffer = Recovering();
        var first = sender.Picture(1, 0, key: true);
        var ready = Push(buffer, first);
        ready.AddRange(Push(buffer, first));
        ready.AddRange(Push(buffer, sender.Picture(2, 0)));
        Assert.That(ready.Select(x => x.FrameId), Is.EqualTo(new[] { 1u, 2u }));
    }

    [Test]
    public void SwitchingRecoveryOnOrOff_SaysSo_SoTheCallerCanAskForAKeyframe()
    {
        var buffer = new VideoRecoveryBuffer();
        Assert.Multiple(() =>
        {
            Assert.That(buffer.Configure(recover: true, 300), Is.True);
            Assert.That(buffer.Configure(recover: true, 500), Is.False, "a new round trip only resizes the window");
            Assert.That(buffer.RecoveryMs, Is.EqualTo(950));
            Assert.That(buffer.Configure(recover: true, 4_000), Is.False);
            Assert.That(buffer.RecoveryMs, Is.EqualTo(VideoRecoveryBuffer.MaxRecoveryMs));
            Assert.That(buffer.Configure(recover: false, 300), Is.True);
            Assert.That(buffer.RecoveryMs, Is.Zero);
        });
    }
}
