using System.Collections.Concurrent;
using System.Diagnostics;
using Bolt.Media.Congestion;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using NUnit.Framework;

namespace Bolt.Tests;

public sealed partial class BoltGroupCallLifecycleTests
{
    [Test]
    public async Task Client_DatagramUpgrade_DiscardsSocketCongestionOnce()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        await using var a = Connect(f, "a", network);
        await using var pacer = new MediaSendPacer((_, _) => ValueTask.CompletedTask, () => 0);
        var controller = new SendRateController(1200);
        var loop = new SendRateLoop(pacer, controller, new VideoRateLadder());
        for (long now = 0; now <= 10_000; now += 250)
        {
            loop.Signals.OnTransportFeedback(new(now, 2000, 1000, 0.3, 60));
            loop.Tick(now);
        }
        Assert.That(loop.VideoSuspended, Is.True);
        var available = 0;
        a.Transport.PathAvailable += () => { Interlocked.Increment(ref available); loop.PathChanged(); };
        a.Transport.Start();
        await WaitUntil(() => Volatile.Read(ref available) > 0);
        var tick = loop.Tick(10_250);
        await Task.Delay(750);
        Assert.Multiple(() =>
        {
            Assert.That(tick.ResumeVideo, Is.True);
            Assert.That(tick.Transport, Is.Null, "the old socket's delay/loss cannot cut the new channel");
            Assert.That(tick.Decision.TotalKbps, Is.GreaterThanOrEqualTo(1200));
            Assert.That(available, Is.EqualTo(1), "path diagnostics must not repeatedly reset adaptation");
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Client_IdleFeedback_SendsOnlyBoundedPaddingAndStopsWhenDisabled(bool dropPackets)
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        await using var a = Connect(f, "a", network);
        var feedback = new ConcurrentQueue<Bolt.Media.Congestion.TransportSignal>();
        a.Transport.TransportFeedback += feedback.Enqueue;
        a.Transport.Start();
        await WaitUntil(() => a.Transport.IsDatagramActive);
        var participant = network.Created.Single(x => x.Role == RtcPeerRole.Offer);
        participant.LoseSent = _ => dropPackets;
        a.Transport.IdleFeedbackEnabled = true;
        await WaitUntil(() => participant.Sent.Count >= 3);
        if (!dropPackets) await WaitUntil(() => feedback.Count >= 3);
        participant.BufferedAmount = 1;
        await Task.Delay(300);
        var busyCount = participant.Sent.Count;
        await Task.Delay(750);
        Assert.That(participant.Sent.Count, Is.EqualTo(busyCount), "no padding behind a busy channel");
        a.Transport.IdleFeedbackEnabled = false;
        participant.BufferedAmount = 0;
        await Task.Delay(300);
        var sent = participant.Sent.ToArray();
        await Task.Delay(750);
        Assert.Multiple(() =>
        {
            Assert.That(sent.Length, Is.InRange(3, 10), "at most two small messages each second");
            Assert.That(participant.Sent.Count, Is.EqualTo(sent.Length), "disabled during normal video or hangup");
            if (dropPackets) Assert.That(feedback, Is.Empty, "a black hole does not produce recovery evidence");
            else Assert.That(feedback.Last().LossFraction, Is.Zero);
            foreach (var message in sent)
            {
                Assert.That(TransportSequenceCodec.TryRead(message, out _, out var inner), Is.True);
                Assert.That(inner.Length, Is.EqualTo(64));
                Assert.That(inner[0], Is.EqualTo((byte)FrameType.Padding), "never microphone or camera content");
            }
        });
    }

    [Test]
    public async Task Client_StartProbe_WaitsForFeedbackAndEchoAcrossAOneSecondRoundTrip()
    {
        var network = new FakeRtcNetwork { Path = new("relay", "udp", "udp", "relay", 1000) };
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        await using var a = Connect(f, "a", network);
        a.Transport.Start();
        await WaitUntil(() => a.Transport.IsDatagramActive);
        var participant = network.Created.Single(x => x.Role == RtcPeerRole.Offer);
        var relay = network.Created.Single(x => x.Role == RtcPeerRole.Answer);
        var pending = new ConcurrentBag<Task>();
        async Task DeliverAfterAsync(FakeRtcPeer peer, byte[] message)
        {
            await Task.Delay(500);
            peer.Deliver(message);
        }
        participant.LoseSent = message =>
        {
            if (!TransportSequenceCodec.TryRead(message, out _, out var inner) || inner[0] != (byte)FrameType.Padding) return false;
            pending.Add(DeliverAfterAsync(relay, message));
            return true;
        };
        relay.LoseSent = message =>
        {
            if (message[0] != (byte)FrameType.Padding && message[0] != (byte)FrameType.TransportFeedback) return false;
            pending.Add(DeliverAfterAsync(participant, message));
            return true;
        };
        var result = await a.Transport.ProbeAsync([600]);
        await Task.WhenAll(pending);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Steps, Has.Count.EqualTo(1));
            Assert.That(result.Steps[0].Inconclusive, Is.False, "valid delayed reports must arrive before judging the step");
            Assert.That(result.Steps[0].UplinkPassed, Is.True);
            Assert.That(result.Steps[0].EchoPassed, Is.True);
            Assert.That(result.UplinkKbps, Is.GreaterThan(400));
            Assert.That(result.DownlinkKbps, Is.GreaterThan(400));
            Assert.That(result.RttMs, Is.GreaterThanOrEqualTo(950));
        });
    }

    [TestCase(1000, 1900)]
    [TestCase(5000, 2900)]
    public async Task Client_StartProbe_WithoutAnyFeedback_RemainsBounded(int rttMs, int minimumWaitMs)
    {
        var network = new FakeRtcNetwork { Path = new("relay", "udp", "udp", "relay", rttMs) };
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        await using var a = Connect(f, "a", network);
        a.Transport.Start();
        await WaitUntil(() => a.Transport.IsDatagramActive);
        network.Created.Single(x => x.Role == RtcPeerRole.Offer).LoseSent = _ => true;
        var elapsed = Stopwatch.StartNew();
        var result = await a.Transport.ProbeAsync([600]);
        Assert.Multiple(() =>
        {
            Assert.That(elapsed.ElapsedMilliseconds, Is.InRange(minimumWaitMs, 4000));
            Assert.That(result!.Steps[0].Inconclusive, Is.True);
            Assert.That(result.UplinkKbps, Is.Zero);
            Assert.That(result.DownlinkKbps, Is.Null, "silence must not invent a capacity measurement");
            Assert.That(a.Transport.Probing, Is.False);
        });
    }
}
