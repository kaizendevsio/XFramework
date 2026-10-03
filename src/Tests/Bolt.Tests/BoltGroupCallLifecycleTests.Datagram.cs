using System.Buffers;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Bolt.Server;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// Phase 3: the relay's side of the datagram media path. A participant negotiates a data channel over its own
/// authenticated socket; the channel replaces only the pipe under that receiver's media lanes, and only media
/// may enter through it. Peers are in-memory fakes (<see cref="FakeRtcNetwork"/>); the relay is real.
/// </summary>
public sealed partial class BoltGroupCallLifecycleTests
{
    private static BoltMediaTransportOptions Transport(FakeRtcNetwork network, FakeIceSource ice, Action<BoltMediaTransportOptionsBuilder>? tune = null)
    {
        var builder = new BoltMediaTransportOptionsBuilder();
        tune?.Invoke(builder);
        return new BoltMediaTransportOptions
        {
            Peers = network.Factory(RtcPeerRole.Answer), IceServers = ice, RequestSpacingSeconds = builder.Spacing,
            MaxRequestsPerConnection = builder.MaxRequests, RenewBefore = builder.RenewBefore,
        };
    }

    internal sealed class BoltMediaTransportOptionsBuilder
    {
        public int Spacing = 0;
        public int MaxRequests = 30;
        public TimeSpan RenewBefore = TimeSpan.FromMinutes(5);
    }

    private static async Task<T> AwaitTransport<T>(Peer peer, MediaTransportKind kind, Func<T, bool>? match = null, int skip = 0) where T : class
    {
        T? found = null;
        await WaitUntil(() =>
        {
            var seen = 0;
            foreach (var frame in peer.Sent)
            {
                if (!MediaTransportCodec.TryRead(frame, out var k, out var payload) || k != kind) continue;
                if (MediaTransportCodec.Decode<T>(payload) is not { } value || (match is not null && !match(value))) continue;
                if (seen++ < skip) continue;
                found = value;
                return true;
            }
            return false;
        });
        return found!;
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition())
        {
            if (Environment.TickCount64 > deadline) Assert.Fail("Timed out waiting for the relay.");
            await Task.Delay(10);
        }
    }

    private static int TransportCount(Peer peer, MediaTransportKind kind) =>
        peer.Sent.Count(x => MediaTransportCodec.TryRead(x, out var k, out _) && k == kind);

    /// <summary>Request, offer and answer like a participant would, until the relay reports the channel open.</summary>
    private static async Task<(FakeRtcPeer Relay, FakeRtcPeer Participant, string Session)> OpenDatagramAsync(Fixture f, FakeRtcNetwork network, string id)
    {
        var peer = f.Peers[id];
        await peer.ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.Request, new MediaTransportRequest(1, RtcDefaults.MaxMessageBytes)));
        var config = await AwaitTransport<MediaTransportConfig>(peer, MediaTransportKind.Config, x => x.Unavailable is null);
        var participant = network.Create(RtcPeerRole.Offer, new RtcPeerOptions(config.IceServers, false, config.MaxMessageBytes));
        var offer = await participant.CreateOfferAsync(false, CancellationToken.None);
        await peer.ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.Offer, new MediaTransportDescription(config.Session, offer)));
        var answer = await AwaitTransport<MediaTransportDescription>(peer, MediaTransportKind.Answer, x => x.Session == config.Session);
        await participant.SetAnswerAsync(answer.Sdp, CancellationToken.None);
        await AwaitTransport<MediaTransportStateMessage>(peer, MediaTransportKind.State, x => x.Session == config.Session && x.State == "open");
        var relay = network.Created.Single(x => x.Role == RtcPeerRole.Answer && ReferenceEquals(x.Partner, participant));
        return (relay, participant, config.Session);
    }

    private static int MediaOn(FakeRtcPeer peer, FrameType type = FrameType.MediaFrame) => peer.Sent.Count(x => x[0] == (byte)type);

    [Test]
    public async Task Datagram_WithoutTurn_TheRelaySaysSoAndEverythingStaysOnTheSocket()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Peers["a"].ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.Request, new MediaTransportRequest(1, 1150)));
        var config = await AwaitTransport<MediaTransportConfig>(f.Peers["a"], MediaTransportKind.Config);
        Assert.Multiple(() =>
        {
            Assert.That(config.Unavailable, Is.EqualTo("disabled"));
            Assert.That(config.IceServers, Is.Empty);
            Assert.That(f.Tasks["a"].IsCompleted, Is.False, "asking is never a reason to drop the connection");
        });
    }

    [Test]
    public async Task Datagram_Negotiation_GivesTheParticipantItsOwnCredentials_AndTheRelayARelayOnlyPeerWithItsOwn()
    {
        var network = new FakeRtcNetwork();
        var ice = new FakeIceSource();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, ice));
        var (relay, participant, session) = await OpenDatagramAsync(f, network, "a");
        Assert.Multiple(() =>
        {
            Assert.That(ice.Requests, Is.EqualTo(new[] { "a" }), "credentials are minted for the authenticated participant");
            Assert.That(participant.Options.IceServers.Single().Username, Is.EqualTo("client-user"));
            Assert.That(relay.Options.IceServers.Single().Username, Is.EqualTo("relay-user"));
            Assert.That(relay.Options.RelayOnly, Is.True);
            Assert.That(relay.Options.MinCwndBytes, Is.EqualTo(128 * 1024));
            Assert.That(relay.Options.MaxMessageBytes, Is.EqualTo(RtcDefaults.MaxMessageBytes));
            Assert.That(relay.RemoteCandidates, Is.Empty, "no candidate was sent yet");
            // The relay's own credential never leaves it: not in a config, an answer or anything else on the socket.
            Assert.That(f.Peers["a"].Sent.Any(x => System.Text.Encoding.UTF8.GetString(x).Contains("relay-secret")), Is.False);
            Assert.That(f.Peers["b"].Sent.Any(x => System.Text.Encoding.UTF8.GetString(x).Contains("client-secret")), Is.False);
            Assert.That(session, Has.Length.EqualTo(24));
        });
        await WaitUntil(() => TransportCount(f.Peers["a"], MediaTransportKind.Candidate) >= 2);
        await f.Peers["a"].ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.Candidate,
            new MediaTransportCandidate(session, "candidate:9 1 udp 1 203.0.113.9 9 typ srflx", "0", 0)));
        await WaitUntil(() => relay.RemoteCandidates.Count == 1);
    }

    [Test]
    public async Task Datagram_OpenChannel_CarriesThatReceiversMedia_WhileConfigurationStaysOnTheSocket()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var (relay, _, _) = await OpenDatagramAsync(f, network, "a");
        var stream = await f.Config("b");
        for (var i = 0; i < 5; i++) await f.Send("b", stream);
        await WaitUntil(() => MediaOn(relay) == 5);
        var hub = Connection(f, "a");
        Assert.Multiple(() =>
        {
            Assert.That(f.Peers["a"].Count(FrameType.MediaFrame), Is.Zero, "media for a takes its data channel");
            Assert.That(f.Peers["a"].Count(FrameType.MediaConfig), Is.EqualTo(1), "configuration stays on the authenticated socket");
            Assert.That(f.Peers["c"].Count(FrameType.MediaFrame), Is.EqualTo(5), "c has no channel and keeps its socket");
            Assert.That(hub.DatagramFramesSent, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task Datagram_Inbound_IsTheConnectionsOwnMedia_AndNothingElse()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var (_, participant, _) = await OpenDatagramAsync(f, network, "a");
        var mine = await f.Config("a");
        var theirs = await f.Config("b");
        Assert.That(participant.TrySend(Frame(w => BoltCodec.WriteMediaFrame(w, mine, 1, 960, 0, [1, 2, 3]))), Is.True);
        // b's stream, signals and configuration may not enter through a's channel.
        participant.TrySend(Frame(w => BoltCodec.WriteMediaFrame(w, theirs, 1, 960, 0, [1])));
        participant.TrySend(Frame(w => BoltCodec.WriteCallSignal(w, f.Call, SignalType.End, [])));
        participant.TrySend(Frame(w => BoltCodec.WriteMediaConfig(w, Guid.NewGuid(), f.Call, MediaType.Audio, CodecId.Opus, 48000, 1, 32, 0, [])));
        await WaitUntil(() => f.Peers["b"].Media(mine).Count == 1);
        await Task.Delay(100);
        Assert.Multiple(() =>
        {
            Assert.That(f.Peers["c"].Media(mine), Has.Count.EqualTo(1));
            Assert.That(f.Peers["a"].Media(theirs), Is.Empty, "a frame of someone else's stream is dropped as on the socket");
            Assert.That(f.Participants(), Does.Contain("a"), "a signal through the channel is ignored, not acted on");
            Assert.That(f.Peers["b"].Count(FrameType.MediaConfig), Is.EqualTo(1), "only the configuration sent on a's socket");
            Assert.That(Connection(f, "a").DatagramRejected, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Datagram_ChannelFailure_ReturnsMediaToTheSocket_AndTellsTheParticipant()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var (relay, _, session) = await OpenDatagramAsync(f, network, "a");
        var stream = await f.Config("b");
        await f.Send("b", stream);
        await WaitUntil(() => MediaOn(relay) == 1);
        relay.Fail();
        await AwaitTransport<MediaTransportStateMessage>(f.Peers["a"], MediaTransportKind.State, x => x.Session == session && x.State == "failed");
        await f.Send("b", stream);
        await WaitUntil(() => f.Peers["a"].Count(FrameType.MediaFrame) == 1);
        Assert.Multiple(() =>
        {
            Assert.That(relay.Disposed, Is.True);
            Assert.That(Connection(f, "a").Datagram, Is.Null);
            Assert.That(f.Tasks["a"].IsCompleted, Is.False, "losing the channel never costs the call");
        });
    }

    [Test]
    public async Task Datagram_FullChannel_HoldsMediaInTheLanes_ThenDrainsWhenItEmpties()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var (relay, _, _) = await OpenDatagramAsync(f, network, "a");
        relay.BufferedAmount = BoltHubConnection.DatagramBacklogLimit(0);
        var stream = await f.Config("b");
        for (var i = 0; i < 4; i++) await f.Send("b", stream);
        await Task.Delay(100);
        var hub = Connection(f, "a");
        Assert.Multiple(() =>
        {
            Assert.That(MediaOn(relay), Is.Zero, "a full channel is not fed: the frames wait where audio can still overtake video");
            Assert.That(f.Peers["a"].Count(FrameType.MediaFrame), Is.Zero, "nor do they leak onto the socket");
            Assert.That(hub.MediaQueue!.QueuedAudioBytes, Is.GreaterThan(0));
        });
        relay.Drain(0);
        await WaitUntil(() => MediaOn(relay) == 4);
    }

    [Test]
    public async Task Datagram_BacklogLimit_FollowsTheCongestionWindow()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BoltHubConnection.DatagramBacklogLimit(0), Is.EqualTo(64 * 1024), "unknown window: a fixed short queue");
            Assert.That(BoltHubConnection.DatagramBacklogLimit(128 * 1024), Is.EqualTo(144 * 1024), "the window plus a short queue");
            Assert.That(BoltHubConnection.DatagramBacklogLimit(4 * 1024), Is.EqualTo(32 * 1024));
            Assert.That(BoltHubConnection.DatagramBacklogLimit(4 * 1024 * 1024), Is.EqualTo(512 * 1024));
        });
        await Task.CompletedTask;
    }

    [Test]
    public async Task Datagram_FramesTooBigForOneMessage_TakeTheSocket()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var (relay, _, _) = await OpenDatagramAsync(f, network, "a");
        var video = await f.VideoConfig("b");
        await f.SendVideo("b", video, keyframe: true, bytes: 4000);
        await f.SendVideo("b", video, keyframe: false, bytes: 600);
        await WaitUntil(() => f.Peers["a"].Count(FrameType.MediaFrame) == 1 && MediaOn(relay) == 1);
        Assert.That(Connection(f, "a").DatagramFallbacks, Is.EqualTo(1));
    }

    [Test]
    public async Task Datagram_ReceiverLoss_AddsAudioRedundancy_OnlyWhileTheLanesAreCalm()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var (relay, _, session) = await OpenDatagramAsync(f, network, "a");
        var stream = await f.Config("b");
        await f.Send("b", stream);
        await WaitUntil(() => MediaOn(relay) == 1);
        await f.Peers["a"].ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.Report, new MediaTransportReport(session, 30)));
        await WaitUntil(() => Connection(f, "a").AudioRedundancy);
        await f.Send("b", stream);
        await WaitUntil(() => MediaOn(relay, FrameType.MediaBundle) == 1);
        var bundle = relay.Sent.Last(x => x[0] == (byte)FrameType.MediaBundle);
        Span<Range> parts = stackalloc Range[MediaBundleCodec.MaxFrames];
        Assert.That(MediaBundleCodec.TryRead(bundle, parts, out var count), Is.True);
        Assert.That(count, Is.EqualTo(2));
        BoltCodec.TryReadMediaFrame(bundle.AsSpan()[parts[0]], out var previous);
        BoltCodec.TryReadMediaFrame(bundle.AsSpan()[parts[1]], out var current);
        Assert.That((previous.SequenceNumber, current.SequenceNumber), Is.EqualTo((1u, 2u)), "the previous frame rides along, oldest first");

        // Low loss for a while turns it off again (hold shortened by sending many calm reports is not possible; check the threshold instead).
        await f.Peers["a"].ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.Report, new MediaTransportReport(session, 10)));
        await Task.Delay(50);
        Assert.That(Connection(f, "a").AudioRedundancy, Is.True, "between the thresholds it stays as it was");
    }

    [Test]
    public async Task Datagram_RedundantAudioFromASender_IsForwardedOnce_AndItsLossIsReported()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var (_, participant, session) = await OpenDatagramAsync(f, network, "a");
        var stream = await f.Config("a");
        byte[] Audio(uint sequence) => Frame(w => BoltCodec.WriteMediaFrame(w, stream, sequence, 960 * sequence, MediaFrameFlags.Encrypted, [1, 2, 3]));
        byte[] Bundle(uint previous, uint current)
        {
            var first = Audio(previous); var second = Audio(current);
            var buffer = new byte[MediaBundleCodec.Size(first.Length, second.Length)];
            MediaBundleCodec.Write(buffer, first, second);
            return buffer;
        }
        participant.TrySend(Audio(1));
        participant.TrySend(Bundle(1, 2));
        // Frame 3 was lost on the way; its copy arrives with frame 4.
        participant.TrySend(Bundle(3, 4));
        participant.TrySend(Audio(6));
        await WaitUntil(() => f.Peers["b"].Media(stream).Count == 5);
        await Task.Delay(50);
        Assert.That(f.Peers["b"].Media(stream).Select(x => x.Sequence), Is.EqualTo(new uint[] { 1, 2, 3, 4, 6 }), "every frame once, in arrival order");
        var report = await AwaitTransport<MediaTransportReport>(f.Peers["a"], MediaTransportKind.Report, x => x.Session == session);
        Assert.That(report.ReceiveLossPermille, Is.EqualTo(166), "1 of 6 frames never arrived");
    }

    [Test]
    public async Task Datagram_ConnectionEnd_ClosesItsPeer()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        var (relay, _, _) = await OpenDatagramAsync(f, network, "a");
        await f.Peers["a"].DisposeAsync();
        await f.Tasks["a"].WaitAsync(TimeSpan.FromSeconds(3));
        await WaitUntil(() => relay.Disposed);
    }

    [Test]
    public async Task Datagram_Requests_AreSpacedAndBounded_AndAProviderFailureIsJustUnavailable()
    {
        var network = new FakeRtcNetwork();
        var ice = new FakeIceSource();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, ice, x => { x.Spacing = 60; }));
        var request = MediaTransportCodec.Encode(MediaTransportKind.Request, new MediaTransportRequest(1, 1150));
        await f.Peers["a"].ProcessAsync(request);
        await f.Peers["a"].ProcessAsync(request);
        await WaitUntil(() => TransportCount(f.Peers["a"], MediaTransportKind.Config) == 2);
        var second = await AwaitTransport<MediaTransportConfig>(f.Peers["a"], MediaTransportKind.Config, skip: 1);
        Assert.Multiple(() =>
        {
            Assert.That(second.Unavailable, Is.EqualTo("rate-limited"));
            Assert.That(ice.Requests, Has.Count.EqualTo(1), "a refused request mints nothing");
        });

        ice.Throws = true;
        await f.Peers["b"].ProcessAsync(request);
        var failed = await AwaitTransport<MediaTransportConfig>(f.Peers["b"], MediaTransportKind.Config);
        Assert.Multiple(() =>
        {
            Assert.That(failed.Unavailable, Is.EqualTo("unavailable"));
            Assert.That(System.Text.Encoding.UTF8.GetString(f.Peers["b"].Sent.Last()), Does.Not.Contain("provider down"));
        });
    }

    [Test]
    public async Task Datagram_ExpiringCredentials_AreRenewedWithANewSessionBeforeTheyRunOut()
    {
        var network = new FakeRtcNetwork();
        var ice = new FakeIceSource { Lifetime = TimeSpan.FromMinutes(2) };
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, ice));
        var (_, _, session) = await OpenDatagramAsync(f, network, "a");
        var renewed = await AwaitTransport<MediaTransportConfig>(f.Peers["a"], MediaTransportKind.Config, x => x.Unavailable is null && x.Session != session);
        Assert.Multiple(() =>
        {
            Assert.That(renewed.IceServers.Single().Username, Is.EqualTo("client-user"));
            Assert.That(ice.Requests, Has.Count.EqualTo(2));
            Assert.That(Connection(f, "a").Datagram, Is.Not.Null, "the current channel carries on until the new one opens");
        });
    }

    [Test]
    public async Task Datagram_AnIceRestartOffer_IsAnsweredOnTheSamePeer()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        var (relay, participant, session) = await OpenDatagramAsync(f, network, "a");
        var offer = await participant.CreateOfferAsync(true, CancellationToken.None);
        await f.Peers["a"].ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.Offer, new MediaTransportDescription(session, offer, IceRestart: true)));
        var answer = await AwaitTransport<MediaTransportDescription>(f.Peers["a"], MediaTransportKind.Answer, x => x.IceRestart);
        Assert.Multiple(() =>
        {
            Assert.That(answer.Session, Is.EqualTo(session));
            Assert.That(network.Created.Count(x => x.Role == RtcPeerRole.Answer), Is.EqualTo(1), "a restart is not a new peer");
            Assert.That(relay.Disposed, Is.False);
        });
    }

    /// <summary>A phone's candidates for one gathering: hosts, server-reflexive and a relay per TURN URL and interface.</summary>
    private static async Task TrickleAsync(Peer peer, string session, int count, int generation)
    {
        for (var i = 0; i < count; i++)
            await peer.ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.Candidate,
                new MediaTransportCandidate(session, $"candidate:{generation}{i} 1 udp 1 203.0.113.{i + 1} {9000 + generation} typ relay", "0", 0)));
    }

    [Test]
    public async Task Datagram_EveryIceRestart_GetsItsOwnCandidates_NotWhatEarlierGatheringsLeftOfACap()
    {
        // With Cloudflare's five browser TURN URLs over Wi-Fi, IPv6 and a VPN interface, a phone gathers 20-30
        // candidates each time. A per-session cap shared by every restart refuses the third gathering outright:
        // pion has dropped the old candidates on restart, so it has nothing to check and the restart can only fail.
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        var (relay, participant, session) = await OpenDatagramAsync(f, network, "a");
        await TrickleAsync(f.Peers["a"], session, 25, 0);
        for (var restart = 1; restart <= 2; restart++)
        {
            var offer = await participant.CreateOfferAsync(true, CancellationToken.None);
            await f.Peers["a"].ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.Offer, new MediaTransportDescription(session, offer, IceRestart: true)));
            await AwaitTransport<MediaTransportDescription>(f.Peers["a"], MediaTransportKind.Answer, x => x.IceRestart, skip: restart - 1);
            await TrickleAsync(f.Peers["a"], session, 25, restart);
        }
        await WaitUntil(() => relay.RemoteCandidates.Count == 75);
    }

    [Test]
    public async Task Datagram_IceRestarts_AreBoundedPerSession_ThenTheSessionEnds_AndTheCallCarriesOn()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        var (relay, participant, session) = await OpenDatagramAsync(f, network, "a");
        for (var restart = 0; restart < 40 && !relay.Disposed; restart++)
        {
            var offer = await participant.CreateOfferAsync(true, CancellationToken.None);
            await f.Peers["a"].ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.Offer, new MediaTransportDescription(session, offer, IceRestart: true)));
        }
        await WaitUntil(() => relay.Disposed);
        Assert.Multiple(() =>
        {
            Assert.That(TransportCount(f.Peers["a"], MediaTransportKind.Answer), Is.LessThan(40), "each restart re-allocates TURN; a phone cannot ask without end");
            Assert.That(Connection(f, "a").Datagram, Is.Null);
            Assert.That(f.Tasks["a"].IsCompleted, Is.False, "the call's socket is untouched");
        });
        await AwaitTransport<MediaTransportClose>(f.Peers["a"], MediaTransportKind.Close, x => x.Session == session);
    }

    [Test]
    public async Task Datagram_APeerTheRelayCannotCreate_IsReportedFailedAtOnce_SoThePhoneDoesNotWaitOutItsTimeout()
    {
        // The sidecar is restarting (or gone): the offer cannot be answered. Saying nothing leaves the phone negotiating
        // for its whole open timeout; saying "failed" lets it carry on over the socket and retry on its backoff.
        var network = new FakeRtcNetwork { FailCreate = true };
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        var peer = f.Peers["a"];
        await peer.ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.Request, new MediaTransportRequest(1, RtcDefaults.MaxMessageBytes)));
        var config = await AwaitTransport<MediaTransportConfig>(peer, MediaTransportKind.Config, x => x.Unavailable is null);
        await peer.ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.Offer, new MediaTransportDescription(config.Session, "offer:x")));
        await AwaitTransport<MediaTransportStateMessage>(peer, MediaTransportKind.State, x => x.Session == config.Session && x.State == "failed");
        Assert.That(f.Tasks["a"].IsCompleted, Is.False);
    }

    [Test]
    public async Task Datagram_TheRelaysCandidates_FollowItsAnswer()
    {
        // Pion gathers as soon as it applies its answer. A candidate that reached the phone before that answer would be
        // added to the previous ICE generation, and an ICE restart drops it with the rest: the restart cannot connect.
        var network = new FakeRtcNetwork { CandidateBeforeAnswer = true };
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        var (_, participant, session) = await OpenDatagramAsync(f, network, "a");
        var offer = await participant.CreateOfferAsync(true, CancellationToken.None);
        await f.Peers["a"].ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.Offer, new MediaTransportDescription(session, offer, IceRestart: true)));
        await AwaitTransport<MediaTransportDescription>(f.Peers["a"], MediaTransportKind.Answer, x => x.IceRestart);
        await WaitUntil(() => TransportCount(f.Peers["a"], MediaTransportKind.Candidate) >= 2);
        var order = f.Peers["a"].Sent.Select(x => MediaTransportCodec.TryRead(x, out var kind, out _) ? kind : (MediaTransportKind?)null)
            .Where(x => x is MediaTransportKind.Answer or MediaTransportKind.Candidate).ToList();
        Assert.That(order.IndexOf(MediaTransportKind.Answer), Is.LessThan(order.IndexOf(MediaTransportKind.Candidate)),
            "every candidate follows the answer it belongs to");
        var restartAnswer = order.LastIndexOf(MediaTransportKind.Answer);
        Assert.That(order.Skip(restartAnswer).Count(x => x == MediaTransportKind.Candidate), Is.GreaterThanOrEqualTo(1),
            "and the restart's candidate is sent after the restart's answer");
    }

    [Test]
    public async Task Datagram_AChannelThatStopsDraining_SendsThatReceiversMediaOnTheSocket()
    {
        // ICE is fine (the phone's checks still arrive), but nothing the relay hands the channel is acknowledged any
        // more: a dead downlink. The buffer never drains, so after a short grace the receiver's media takes its socket.
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var (relay, _, _) = await OpenDatagramAsync(f, network, "a");
        relay.Stuck = true;
        var stream = await f.Config("b");
        var started = Environment.TickCount64;
        while (Environment.TickCount64 - started < 3_000 && f.Peers["a"].Count(FrameType.MediaFrame) == 0)
        {
            await f.Send("b", stream);
            await Task.Delay(20);
        }
        Assert.Multiple(() =>
        {
            Assert.That(f.Peers["a"].Count(FrameType.MediaFrame), Is.GreaterThan(0), "media left the stalled channel for the socket");
            Assert.That(f.Tasks["a"].IsCompleted, Is.False);
        });
    }

    [Test]
    public async Task Datagram_AParticipantReportingItsChannelStalled_GetsItsMediaOnTheSocket_UntilItIsBack()
    {
        // The phone hears nothing on its channel (its ICE went quiet) while the relay still hears the phone: only the
        // phone knows the downlink is dead. It says so over its socket, and says so again when the channel is back.
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var (relay, _, session) = await OpenDatagramAsync(f, network, "a");
        var stream = await f.Config("b");
        await f.Peers["a"].ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.State, new MediaTransportStateMessage(session, "stalled")));
        await f.Send("b", stream);
        await WaitUntil(() => f.Peers["a"].Count(FrameType.MediaFrame) == 1);
        Assert.That(MediaOn(relay), Is.Zero);
        await f.Peers["a"].ProcessAsync(MediaTransportCodec.Encode(MediaTransportKind.State, new MediaTransportStateMessage(session, "open")));
        await f.Send("b", stream);
        await WaitUntil(() => MediaOn(relay) == 1);
        Assert.That(f.Peers["a"].Count(FrameType.MediaFrame), Is.EqualTo(1));
    }

    [Test]
    public async Task Datagram_AChannelThatFlaps_DoesNotDragMediaBackAndForth()
    {
        // Production 12:47-12:49 UTC: the channel stalled and recovered every few seconds, and every time the relay
        // moved that receiver's media back onto it. Each switch reorders or loses frames and costs a keyframe.
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var (relay, _, _) = await OpenDatagramAsync(f, network, "a");
        var stream = await f.Config("b");
        var started = Environment.TickCount64;
        var tick = 0;
        while (Environment.TickCount64 - started < 4_000)
        {
            if (tick % 10 == 0) relay.Stall();
            if (tick % 10 == 3) relay.Recover();
            await f.Send("b", stream);
            tick++;
            await Task.Delay(20);
        }
        var onChannel = relay.Sent.Where(x => x[0] == (byte)FrameType.MediaFrame && BoltCodec.TryReadMediaFrame(x, out _))
            .Select(x => { BoltCodec.TryReadMediaFrame(x, out var h); return h.SequenceNumber; }).ToHashSet();
        var all = onChannel.Concat(f.Peers["a"].Media(stream).Select(x => x.Sequence)).Order().ToList();
        var switches = all.Zip(all.Skip(1)).Count(x => onChannel.Contains(x.First) != onChannel.Contains(x.Second));
        Assert.That(switches, Is.LessThanOrEqualTo(2), "one move to the socket, at most one back: not one per blip");
    }

    private static BoltHubConnection Connection(Fixture f, string id)
    {
        var connections = (System.Collections.Concurrent.ConcurrentDictionary<string, BoltHubConnection>)typeof(BoltServer)
            .GetField("_connectionsByStreamId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(f.Server)!;
        return connections.Values.Single(x => x.ClientId == id);
    }
}
