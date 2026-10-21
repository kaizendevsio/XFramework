using Bolt.Media;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using NUnit.Framework;

namespace Bolt.Tests;

public sealed partial class BoltGroupCallLifecycleTests
{
    private static readonly MediaTransportClientOptions DirectClient = new()
    {
        DirectPeer = true, OpenTimeout = TimeSpan.FromSeconds(3),
        FirstRetryDelay = TimeSpan.FromMilliseconds(200), PathHysteresis = QuickPath,
    };

    [Test]
    public async Task DirectPeers_CarryMediaFeedbackAndRepairsWithoutServer_ThenReturnForThirdParticipant()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(["a", "b", "c"]);
        await f.Join("a"); await f.Join("b");
        var stream = await f.Config("a");
        await using var a = Connect(f, "a", network, DirectClient);
        await using var b = Connect(f, "b", network, DirectClient);
        var received = 0; var keyRequests = 0; var reports = 0;
        b.Client.RegisterFrameHandler(FrameType.MediaFrame, (_, _, _) => Interlocked.Increment(ref received));
        a.Client.RegisterFrameHandler(FrameType.MediaKeyRequest, (_, _, _) => Interlocked.Increment(ref keyRequests));
        a.Transport.TransportFeedback += _ => Interlocked.Increment(ref reports);
        a.Transport.Start(); b.Transport.Start();
        await WaitUntil(() => a.Transport.Status.Topology == "peer-turn" && b.Transport.Status.Topology == "peer-turn", 7000);
        var data = Frame(w => BoltCodec.WriteMediaFrame(w, stream, 1, 960, MediaFrameFlags.Encrypted, [1, 2, 3]));
        Assert.That(a.Transport.TrySend(data), Is.True);
        await WaitUntil(() => received == 1 && reports > 0);
        Assert.That(f.Peers["b"].Media(stream), Is.Empty, "media never traversed the server's socket");
        Assert.That(b.Transport.TrySend(Frame(w => BoltCodec.WriteMediaKeyRequest(w, stream))), Is.True);
        await WaitUntil(() => keyRequests == 1);
        var aPeer = (FakeRtcPeer)a.Transport.ActivePeer!;
        var bPeer = (FakeRtcPeer)b.Transport.ActivePeer!;
        Assert.That(aPeer.Remote, Is.SameAs(bPeer), "the two browser peers connect to each other");
        Assert.That(aPeer.Options.RelayOnly, Is.False);
        var dropped = false;
        aPeer.LoseSent = packet => packet[0] == (byte)FrameType.TransportSequenced && !dropped && (dropped = true);
        Assert.That(a.Transport.TrySend(data), Is.True);
        Assert.That(a.Transport.TrySend(data), Is.True);
        await WaitUntil(() => received == 3, 3000);
        Assert.That(dropped, Is.True, "the missing frame was recovered using the other phone's arrival report");
        await f.Join("c");
        await WaitUntil(() => a.Transport.Status.Topology == "server" && b.Transport.Status.Topology == "server", 5000);
        Assert.That(aPeer.Disposed && bPeer.Disposed, Is.True);
    }

    [Test]
    public async Task DirectPeers_FailureClosesBothPeers_AndRestoresServerRouting()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(["a", "b"]);
        await f.Join("a"); await f.Join("b");
        var stream = await f.Config("a");
        await using var a = Connect(f, "a", network, DirectClient);
        await using var b = Connect(f, "b", network, DirectClient);
        a.Transport.Start(); b.Transport.Start();
        await WaitUntil(() => a.Transport.Status.Topology == "peer-turn" && b.Transport.Status.Topology == "peer-turn", 7000);
        var oldA = (FakeRtcPeer)a.Transport.ActivePeer!;
        var oldB = (FakeRtcPeer)b.Transport.ActivePeer!;
        var probe = a.Transport.ProbeAsync([5000], stepMs: 1000);
        oldA.Fail();
        Assert.That(await probe, Is.Null, "a probe must not combine measurements from two routes");
        await WaitUntil(() => oldA.Disposed && oldB.Disposed);
        await WaitUntil(() => a.Transport.IsDatagramActive && b.Transport.IsDatagramActive &&
            a.Transport.Status.Topology == "server" && b.Transport.Status.Topology == "server", 7000);
        Assert.That(((FakeRtcPeer)a.Transport.ActivePeer!).Remote, Is.Not.SameAs(b.Transport.ActivePeer));
        var received = 0;
        b.Client.RegisterFrameHandler(FrameType.MediaFrame, (_, _, _) => Interlocked.Increment(ref received));
        Assert.That(a.Transport.TrySend(Frame(w => BoltCodec.WriteMediaFrame(w, stream, 1, 960, MediaFrameFlags.Encrypted, [1, 2, 3]))), Is.True);
        await WaitUntil(() => received == 1);
    }

    [Test]
    public async Task DirectPeers_AuthorizationRevocationClosesThePeerRoute()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(["a", "b"]);
        await f.Join("a"); await f.Join("b");
        await using var a = Connect(f, "a", network, DirectClient);
        await using var b = Connect(f, "b", network, DirectClient);
        a.Transport.Start(); b.Transport.Start();
        await WaitUntil(() => a.Transport.Status.Topology == "peer-turn" && b.Transport.Status.Topology == "peer-turn", 7000);
        var oldA = (FakeRtcPeer)a.Transport.ActivePeer!;
        var oldB = (FakeRtcPeer)b.Transport.ActivePeer!;
        f.Policy.Accepted.Remove("b");
        await WaitUntil(() => oldA.Disposed && oldB.Disposed, 7000);
    }

    [Test]
    public async Task DirectPeers_RequireBothClientsToAdvertiseSupport()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(["a", "b"]);
        await f.Join("a"); await f.Join("b");
        await using var a = Connect(f, "a", network, DirectClient);
        await using var b = Connect(f, "b", network, QuickClient);
        a.Transport.Start(); b.Transport.Start();
        await WaitUntil(() => a.Transport.IsDatagramActive && b.Transport.IsDatagramActive);
        await Task.Delay(1200);
        Assert.That(a.Transport.Status.Topology, Is.EqualTo("server"));
        Assert.That(b.Transport.Status.Topology, Is.EqualTo("server"));
    }
}
