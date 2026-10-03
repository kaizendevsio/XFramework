using Bolt.Protocol;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// Selective retransmission on datagram paths, relay side: a receiver's NACK is served from the relay's copy when the
/// relay had sent the frame (one hop, that receiver alone), forwarded to the sender when the sender's uplink lost it,
/// and declined when the relay dropped it on purpose, so the receiver does not wait for it.
/// </summary>
public sealed partial class BoltGroupCallLifecycleTests
{
    private static byte[] Nack(Guid stream, params uint[] sequences) => Frame(w => BoltCodec.WriteNackRequest(w, stream, sequences));

    private static byte[] VideoFrame(Guid stream, uint sequence, bool keyframe = false, int layer = 0) =>
        Frame(w => BoltCodec.WriteMediaFrame(w, stream, sequence, 3000 * sequence,
            MediaFrameFlags.WithTemporalLayer(keyframe ? MediaFrameFlags.Keyframe : (byte)0, layer), new byte[600]));

    private static List<uint> MediaSequences(IEnumerable<byte[]> frames, Guid stream) => frames
        .Where(x => x[0] == (byte)FrameType.MediaFrame && BoltCodec.TryReadMediaFrame(x, out var h) && h.StreamId == stream)
        .Select(x => { BoltCodec.TryReadMediaFrame(x, out var h); return h.SequenceNumber; }).ToList();

    private static List<uint[]> SequenceLists(IEnumerable<byte[]> frames, FrameType type) => frames
        .Where(x => x[0] == (byte)type && BoltCodec.TryReadNackRequest(x, out _))
        .Select(x => { BoltCodec.TryReadNackRequest(x, out var h); return h.GetMissingSequences(x); }).ToList();

    [Test]
    public async Task Datagram_TheRelayAnnouncesNack()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        await f.Peers["a"].ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.Request, new MediaTransportRequest(1, 1150)));
        var config = await AwaitTransport<MediaTransportConfig>(f.Peers["a"], MediaTransportKind.Config, x => x.Unavailable is null);
        Assert.That(MediaTransportFeatures.Has(config, MediaTransportFeatures.Nack), Is.True);
    }

    [Test]
    public async Task Datagram_AFrameLostAfterTheRelay_IsResentByTheRelay_ToThatReceiverAlone()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var (relay, participant, _) = await OpenDatagramAsync(f, network, "a");
        var stream = await f.VideoConfig("b");
        for (uint sequence = 1; sequence <= 5; sequence++) await f.Peers["b"].ProcessAsync(VideoFrame(stream, sequence, keyframe: sequence == 1));
        await WaitUntil(() => MediaSequences(relay.Sent, stream).Count == 5);

        // Frame 3 never reached a's device.
        Assert.That(participant.TrySend(Nack(stream, 3)), Is.True);
        await WaitUntil(() => MediaSequences(relay.Sent, stream).Count == 6);
        await Task.Delay(50);
        Assert.Multiple(() =>
        {
            Assert.That(MediaSequences(relay.Sent, stream), Is.EqualTo(new uint[] { 1, 2, 3, 4, 5, 3 }));
            Assert.That(SequenceLists(f.Peers["b"].Sent, FrameType.NackRequest), Is.Empty, "the sender's uplink is not asked to resend");
            Assert.That(MediaSequences(f.Peers["c"].Sent, stream), Has.Count.EqualTo(5), "nobody else gets it twice");
            Assert.That(Connection(f, "a").RetransmittedFrames, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Datagram_AFrameTheSendersUplinkLost_IsAskedOfTheSender_AndItsResendGoesToWhoeverLacksIt()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var (relay, participant, _) = await OpenDatagramAsync(f, network, "a");
        var stream = await f.VideoConfig("b");
        foreach (var sequence in new uint[] { 1, 2, 4, 5 }) await f.Peers["b"].ProcessAsync(VideoFrame(stream, sequence, keyframe: sequence == 1));
        await WaitUntil(() => MediaSequences(relay.Sent, stream).Count == 4);

        participant.TrySend(Nack(stream, 3));
        await WaitUntil(() => SequenceLists(f.Peers["b"].Sent, FrameType.NackRequest).Count == 1);
        Assert.That(SequenceLists(f.Peers["b"].Sent, FrameType.NackRequest)[0], Is.EqualTo(new uint[] { 3 }));

        // The sender resends it; the relay's lanes have moved past 3, so it goes straight to the channel that lacks it.
        await f.Peers["b"].ProcessAsync(VideoFrame(stream, 3));
        await WaitUntil(() => MediaSequences(relay.Sent, stream).Count == 5);
        await f.Peers["b"].ProcessAsync(VideoFrame(stream, 3));
        await Task.Delay(50);
        Assert.Multiple(() =>
        {
            Assert.That(MediaSequences(relay.Sent, stream), Is.EqualTo(new uint[] { 1, 2, 4, 5, 3 }), "once: a second copy is a duplicate");
            Assert.That(MediaSequences(f.Peers["c"].Sent, stream), Is.EqualTo(new uint[] { 1, 2, 4, 5 }),
                "c is on TCP, where a resent frame behind newer ones is useless, as before");
        });
    }

    [Test]
    public async Task Datagram_AFrameTheRelayDroppedOnPurpose_IsDeclined_NotResentAndNotAskedOfTheSender()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var (relay, participant, _) = await OpenDatagramAsync(f, network, "a");
        var stream = await f.VideoConfig("b");
        // A delta before the first keyframe: undecodable for a new receiver, so the relay never sends it.
        await f.Peers["b"].ProcessAsync(VideoFrame(stream, 1));
        await f.Peers["b"].ProcessAsync(VideoFrame(stream, 2, keyframe: true));
        await f.Peers["b"].ProcessAsync(VideoFrame(stream, 3));
        await WaitUntil(() => MediaSequences(relay.Sent, stream).Count == 2);

        participant.TrySend(Nack(stream, 1));
        await WaitUntil(() => SequenceLists(relay.Sent, FrameType.NackDeclined).Count == 1);
        await Task.Delay(50);
        Assert.Multiple(() =>
        {
            Assert.That(SequenceLists(relay.Sent, FrameType.NackDeclined)[0], Is.EqualTo(new uint[] { 1 }));
            Assert.That(MediaSequences(relay.Sent, stream), Is.EqualTo(new uint[] { 2, 3 }));
            Assert.That(SequenceLists(f.Peers["b"].Sent, FrameType.NackRequest), Is.Empty);
        });
    }

    [Test]
    public async Task Datagram_WithNackOff_TheRelayAnnouncesNothing_AndForwardsNacksAsBefore()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o =>
        {
            var transport = Transport(network, new FakeIceSource());
            o.MediaTransport = new Bolt.Server.BoltMediaTransportOptions
            {
                Peers = transport.Peers, IceServers = transport.IceServers, RequestSpacingSeconds = 0, Nack = false,
            };
        });
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        await f.Peers["a"].ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.Request, new MediaTransportRequest(1, 1150)));
        var config = await AwaitTransport<MediaTransportConfig>(f.Peers["a"], MediaTransportKind.Config, x => x.Unavailable is null);
        Assert.That(MediaTransportFeatures.Has(config, MediaTransportFeatures.Nack), Is.False);
    }
}
