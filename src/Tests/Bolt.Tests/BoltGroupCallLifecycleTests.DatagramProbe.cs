using System.Collections.Concurrent;
using System.Diagnostics;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using NUnit.Framework;

namespace Bolt.Tests;

public sealed partial class BoltGroupCallLifecycleTests
{
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
