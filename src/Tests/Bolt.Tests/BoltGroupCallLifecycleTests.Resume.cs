using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Claims;
using Bolt.Protocol;
using Bolt.Server;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// What a resumable call needs from the relay: a liveness echo a phone can time, a departure reason
/// the host can tell a closed socket from a refusal with, and a room that takes the same participant
/// back on a new connection within the same key epoch.
/// </summary>
public sealed partial class BoltGroupCallLifecycleTests
{
    [Test]
    public async Task Heartbeat_IsEchoedToItsSenderAlone_ByteForByte()
    {
        await using var f = await Fixture.CreateAsync();
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var probe = Heartbeat(f.Call, 1234567890123);

        await f.Peers["a"].ProcessAsync(probe);

        Assert.That(() => f.Peers["a"].Signals(SignalType.Heartbeat), Is.EqualTo(1).After(2000, 10));
        Assert.Multiple(() =>
        {
            Assert.That(f.Peers["a"].Sent.Single(x => IsHeartbeat(x)), Is.EqualTo(probe), "the echo is the probe, unchanged");
            Assert.That(f.Peers["b"].Signals(SignalType.Heartbeat), Is.Zero, "nobody else learns anything from a probe");
            Assert.That(f.Peers["c"].Signals(SignalType.Heartbeat), Is.Zero);
            Assert.That(f.Tasks["a"].IsCompleted, Is.False, "a heartbeat is a media-protocol frame the relay accepts");
        });
    }

    [Test]
    public async Task Heartbeat_IsAnsweredBeforeAndOutsideAnyCall()
    {
        await using var f = await Fixture.CreateAsync();
        // Nobody joined: a phone still has to know its socket works while it waits to be admitted.
        await f.Peers["c"].ProcessAsync(Heartbeat(Guid.NewGuid(), 7));
        Assert.That(() => f.Peers["c"].Signals(SignalType.Heartbeat), Is.EqualTo(1).After(2000, 10));
    }

    [Test]
    public async Task Heartbeat_OversizedOrFlooding_IsNotAmplified()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Peers["a"].ProcessAsync(Frame(w => BoltCodec.WriteCallSignal(w, f.Call, SignalType.Heartbeat, new byte[BoltServer.MaxHeartbeatPayloadBytes + 1])));
        for (var i = 0; i < 10; i++) await f.Peers["b"].ProcessAsync(Heartbeat(f.Call, i));
        await Task.Delay(300);
        Assert.Multiple(() =>
        {
            Assert.That(f.Peers["a"].Signals(SignalType.Heartbeat), Is.Zero, "a probe larger than a stamp is not echoed");
            Assert.That(f.Peers["b"].Signals(SignalType.Heartbeat), Is.EqualTo(1), "a burst of probes gets one echo per spacing window");
        });
        await Task.Delay(BoltServer.MinHeartbeatSpacingMs + 50);
        await f.Peers["b"].ProcessAsync(Heartbeat(f.Call, 99));
        Assert.That(() => f.Peers["b"].Signals(SignalType.Heartbeat), Is.EqualTo(2).After(2000, 10));
    }

    [TestCase("end", BoltGroupDepartureReason.Left)]
    [TestCase("host", BoltGroupDepartureReason.Left)]
    [TestCase("disconnect", BoltGroupDepartureReason.Disconnected)]
    [TestCase("revoked", BoltGroupDepartureReason.Unauthorized)]
    public async Task Departure_SaysWhyTheParticipantLeft(string how, BoltGroupDepartureReason expected)
    {
        await using var f = await Fixture.CreateAsync();
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var departures = new ConcurrentQueue<BoltGroupDeparture>();
        var removed = new ConcurrentQueue<string>();
        f.Server.GroupParticipantDeparted += departures.Enqueue;
        f.Server.GroupParticipantRemoved += (_, id) => removed.Enqueue(id);
        var stream = await f.Config("a");

        switch (how)
        {
            case "end": await f.Peers["c"].ProcessAsync(Frame(w => BoltCodec.WriteCallSignal(w, f.Call, SignalType.End, []))); break;
            case "host": await f.Server.LeaveGroupCallAsync(f.Call, "c"); break;
            case "disconnect": await f.Peers["c"].DisposeAsync(); break;
            case "revoked":
                f.Policy.Accepted.Remove("c");
                f.ExpireLease();
                await f.Send("a", stream);
                break;
        }

        Assert.That(() => departures.Count, Is.EqualTo(1).After(5000, 10));
        Assert.Multiple(() =>
        {
            Assert.That(departures.Single() with { Participant = null }, Is.EqualTo(new BoltGroupDeparture(f.Call, "c", expected)));
            Assert.That(departures.Single().Participant?.FindFirst("bolt_media_client_id")?.Value, Is.EqualTo("c"), "the departed connection's own principal");
            Assert.That(removed, Is.EqualTo(new[] { "c" }), "the reason-less notification still fires for existing hosts");
        });
    }

    /// <summary>
    /// The relay half of a resume: the same participant, on a brand-new connection, is admitted again,
    /// is replayed every live stream configuration first, republishes under a new stream, and the other
    /// participants never lose each other in the meantime.
    /// </summary>
    [Test]
    public async Task Rejoin_OnANewConnection_ReplaysConfigsAndRoutesTheRepublishedStream()
    {
        await using var f = await Fixture.CreateAsync();
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var aStream = await f.Config("a");
        var cOld = await f.Config("c");
        await f.Send("c", cOld);
        Assert.That(() => f.Peers["a"].Media(cOld), Has.Count.EqualTo(1).After(2000, 10));

        await f.Peers["c"].DisposeAsync();
        await f.Tasks["c"].WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(() => f.Peers["a"].Signals(SignalType.StreamEnded), Is.EqualTo(1).After(2000, 10));
        Assert.That(f.Participants(), Is.EquivalentTo(new[] { "a", "b" }), "a and b keep their room while c is away");
        await f.Send("a", aStream);
        Assert.That(() => f.Peers["b"].Media(aStream), Has.Count.EqualTo(1).After(2000, 10));

        var fresh = await ReconnectAsync(f, "c");
        Assert.That(await f.Join("c"), Is.True, "the relay admits the participant again on its new connection");
        var cNew = await f.Config("c");
        await f.Send("c", cNew);
        await f.Send("a", aStream);

        Assert.Multiple(() =>
        {
            Assert.That(() => fresh.Media(aStream), Has.Count.EqualTo(1).After(2000, 10));
            Assert.That(fresh.Sent.Where(x => x[0] is (byte)FrameType.MediaConfig or (byte)FrameType.MediaFrame).Select(x => (FrameType)x[0]).First(),
                Is.EqualTo(FrameType.MediaConfig), "a stream's configuration reaches the resumed connection before its media");
            Assert.That(() => f.Peers["a"].Media(cNew), Has.Count.EqualTo(1).After(2000, 10));
            Assert.That(() => f.Peers["b"].Media(cNew), Has.Count.EqualTo(1).After(2000, 10));
            Assert.That(f.Participants(), Is.EquivalentTo(new[] { "a", "b", "c" }));
        });
    }

    /// <summary>
    /// Phases 1 and 2 together: a sender that resumes on a new connection republishes its camera, and the
    /// relay's congestion reports (0x27) and heartbeat echoes (0x0E) follow it to that connection. The old
    /// connection hears nothing more, and receivers get the new stream from a keyframe.
    /// </summary>
    [Test]
    public async Task ResumedSender_GetsCongestionReportsAndHeartbeatsOnItsNewConnection()
    {
        await using var f = await Fixture.CreateAsync();
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var before = await f.VideoConfig("a");
        await f.SendVideo("a", before, keyframe: true);
        Assert.That(() => f.Peers["a"].Congestion(before), Is.Not.Empty.After(3000, 10));

        var old = f.Peers["a"];
        await old.DisposeAsync();
        await f.Tasks["a"].WaitAsync(TimeSpan.FromSeconds(3));
        var oldFrames = old.Sent.Count;
        var fresh = await ReconnectAsync(f, "a");
        Assert.That(await f.Join("a"), Is.True);
        var after = await f.VideoConfig("a");
        await f.SendVideo("a", after, keyframe: true);
        await f.SendVideo("a", after, keyframe: false);
        await fresh.ProcessAsync(Heartbeat(f.Call, 42));

        Assert.Multiple(() =>
        {
            Assert.That(() => fresh.Congestion(after), Is.Not.Empty.After(3000, 10), "reports reach the resumed connection");
            Assert.That(() => fresh.Signals(SignalType.Heartbeat), Is.EqualTo(1).After(2000, 10), "the heartbeat is echoed on it");
            Assert.That(() => f.Peers["b"].Media(after).Select(x => x.Keyframe).FirstOrDefault(), Is.True.After(3000, 10),
                "the republished stream starts on a keyframe");
            Assert.That(fresh.Congestion(before), Is.Empty, "nothing about the retired stream reaches the new connection");
        });
        Assert.That(old.Sent.Count, Is.EqualTo(oldFrames), "the dead connection is sent nothing more");
    }

    /// <summary>Give <paramref name="id"/> a new connection, the way a phone opens a new socket after a resume ticket.</summary>
    private static async Task<Peer> ReconnectAsync(Fixture f, string id)
    {
        var peer = new Peer();
        f.Peers[id] = peer;
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", id), new Claim("bolt_media_client_id", id)], "fixture"));
        f.Tasks[id] = f.Server.HandleConnectionAsync(peer, principal, CancellationToken.None, isSecureTransport: true);
        await peer.ProcessAsync(Frame(w => BoltCodec.WriteRegister(w, id, id)));
        Assert.That(() => peer.Sent.Any(x => x[0] == (byte)FrameType.RegisterAck && x[1] == 1), Is.True.After(2000, 10),
            "the new connection registers once the old one is gone");
        return peer;
    }

    private static byte[] Heartbeat(Guid call, long stamp)
    {
        var payload = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(payload, stamp);
        return Frame(w => BoltCodec.WriteCallSignal(w, call, SignalType.Heartbeat, payload));
    }

    private static bool IsHeartbeat(byte[] frame) =>
        BoltCodec.TryReadCallSignal(frame, out var header) && header.SignalType == SignalType.Heartbeat;
}
