using System.Reflection;
using System.Threading.Channels;
using Bolt.Client;
using Bolt.Media;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// Phase 3, the participant's side: <see cref="MediaTransportClient"/> against the real relay over an in-memory
/// socket, with fake WebRTC peers. Covers selection, fallback, upgrade, ICE restart, renewal and redundancy.
/// </summary>
public sealed partial class BoltGroupCallLifecycleTests
{
    private static readonly MediaTransportClientOptions FastClient = new()
    {
        OpenTimeout = TimeSpan.FromMilliseconds(600), FirstRetryDelay = TimeSpan.FromMilliseconds(300),
        MaxRetryDelay = TimeSpan.FromSeconds(1), ReportInterval = TimeSpan.FromMilliseconds(200),
    };

    /// <summary><see cref="FastClient"/> that leaves and returns without hysteresis, for tests of those mechanics.</summary>
    private static readonly MediaTransportClientOptions QuickClient = new()
    {
        OpenTimeout = TimeSpan.FromMilliseconds(600), FirstRetryDelay = TimeSpan.FromMilliseconds(300),
        MaxRetryDelay = TimeSpan.FromSeconds(1), ReportInterval = TimeSpan.FromMilliseconds(200), PathHysteresis = QuickPath,
    };

    private sealed class Participant : IAsyncDisposable
    {
        public required BoltClient Client { get; init; }
        public required MediaTransportClient Transport { get; init; }
        public required BridgeTransport Bridge { get; init; }
        public async ValueTask DisposeAsync()
        {
            await Transport.DisposeAsync();
            Bridge.Close();
            await Client.DisposeAsync();
        }
    }

    /// <summary>A participant's client whose socket is the fixture's in-memory connection for <paramref name="id"/>.</summary>
    private static Participant Connect(Fixture f, string id, FakeRtcNetwork network, MediaTransportClientOptions? options = null, Func<long>? clock = null)
    {
        var client = new BoltClient(new Uri("wss://localhost/bolt"), id, id, new BoltClientOptions(), NullLogger<BoltClient>.Instance);
        var bridge = new BridgeTransport(f.Peers[id]);
        var connection = new BoltConnection(bridge);
        connection.StartSendLoop(CancellationToken.None);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        ((List<BoltConnection>)typeof(BoltClient).GetField("_connections", flags)!.GetValue(client)!).Add(connection);
        typeof(BoltClient).GetField("_isRegistered", flags)!.SetValue(client, true);
        connection.ReceiveLoop = (Task)typeof(BoltClient).GetMethod("ReceiveLoopAsync", flags)!.Invoke(client, [connection, CancellationToken.None])!;
        var factory = network.Factory(RtcPeerRole.Offer);
        var transport = new MediaTransportClient(client, (peerOptions, ct) => factory.CreateAsync(RtcPeerRole.Offer, peerOptions, ct), NullLogger.Instance, options ?? FastClient, clock);
        return new Participant { Client = client, Transport = transport, Bridge = bridge };
    }

    /// <summary>The participant's end of the fixture's in-memory socket.</summary>
    private sealed class BridgeTransport : IBoltConnection
    {
        private readonly Channel<byte[]> _inbound = Channel.CreateUnbounded<byte[]>();
        private readonly Peer _relaySide;
        private bool _closed;

        public BridgeTransport(Peer relaySide)
        {
            _relaySide = relaySide;
            _relaySide.Forward = frame => _inbound.Writer.TryWrite(frame);
        }

        public bool SupportsDatagrams => false;
        public bool IsConnected => !_closed;
        public BoltTransport TransportType => BoltTransport.WebSocket;
        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) { _relaySide.Send(data.ToArray()); return ValueTask.CompletedTask; }
        public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            try
            {
                var frame = await _inbound.Reader.ReadAsync(ct);
                frame.CopyTo(buffer);
                return (frame.Length, true);
            }
            catch (ChannelClosedException) { return (0, true); }
        }
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => throw new NotSupportedException();
        public void Close() { _closed = true; _inbound.Writer.TryComplete(); }
        public ValueTask CloseAsync(CancellationToken ct = default) { Close(); return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() { Close(); return ValueTask.CompletedTask; }
    }

    [Test]
    public async Task Client_MovesMediaToTheDatagramPath_AndBackToTheSocketWhenItFails_ThenUpgradesAgain()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var stream = await f.Config("a");
        await using var a = Connect(f, "a", network, QuickClient);
        var changes = new List<MediaPathStatus>();
        a.Transport.StatusChanged += status => { lock (changes) changes.Add(status); };
        var audio = Frame(w => BoltCodec.WriteMediaFrame(w, stream, 1, 960, MediaFrameFlags.Encrypted, [1, 2, 3]));
        Assert.That(a.Transport.TrySend(audio), Is.False, "before the channel opens, the caller sends on the socket");

        a.Transport.Start();
        await WaitUntil(() => a.Transport.IsDatagramActive);
        Assert.Multiple(() =>
        {
            Assert.That(a.Transport.Status.Kind, Is.EqualTo(MediaPathKind.Datagram));
            Assert.That(a.Transport.Status.Description, Is.EqualTo("UDP/relay"));
            Assert.That(a.Transport.MaxMessageBytes, Is.EqualTo(RtcDefaults.MaxMessageBytes - TransportSequenceCodec.HeaderSize),
                "every message carries the 3-byte transport stamp this relay reports on");
            Assert.That(a.Transport.TrySend(audio), Is.True);
        });
        await WaitUntil(() => f.Peers["b"].Media(stream).Count == 1);

        // The relay's side of the channel fails (say the TURN allocation dies): media returns to the socket at once.
        network.Created.Single(x => x.Role == RtcPeerRole.Answer).Fail();
        await WaitUntil(() => !a.Transport.IsDatagramActive);
        Assert.That(a.Transport.Status.Reason, Does.StartWith("relay-").Or.EqualTo("closed"));
        Assert.That(a.Transport.TrySend(audio), Is.False);

        // And after the backoff the client tries again and is back on UDP.
        await WaitUntil(() => a.Transport.IsDatagramActive, 5000);
        Assert.That(network.Created.Count(x => x.Role == RtcPeerRole.Answer), Is.EqualTo(2));
        lock (changes) Assert.That(changes.Select(x => x.Kind), Does.Contain(MediaPathKind.WebSocket).And.Contain(MediaPathKind.Datagram));
    }

    [Test]
    public async Task Client_WhenUdpIsBlocked_GivesUpOnTheChannelAndStaysOnTheSocket()
    {
        var network = new FakeRtcNetwork { Reachable = false };
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        await using var a = Connect(f, "a", network);
        a.Transport.Start();
        await WaitUntil(() => a.Transport.Status.Reason == "timeout", 4000);
        Assert.Multiple(() =>
        {
            Assert.That(a.Transport.IsDatagramActive, Is.False);
            Assert.That(a.Transport.Status.Kind, Is.Not.EqualTo(MediaPathKind.Datagram));
            Assert.That(f.Tasks["a"].IsCompleted, Is.False, "the call's socket is untouched");
        });
        // The relay is told, and closes its half.
        await WaitUntil(() => network.Created.Where(x => x.Role == RtcPeerRole.Answer).All(x => x.Disposed) &&
                              network.Created.Any(x => x.Role == RtcPeerRole.Answer));
    }

    [Test]
    public async Task Client_ARelayWithoutTurn_IsAskedOnce()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync();
        await using var a = Connect(f, "a", network);
        a.Transport.Start();
        await WaitUntil(() => a.Transport.Status.Reason == "disabled");
        await Task.Delay(1000);
        Assert.Multiple(() =>
        {
            Assert.That(f.Peers["a"].Sent.Count(x => MediaTransportCodec.TryRead(x, out var k, out _) && k == MediaTransportKind.Config), Is.EqualTo(1));
            Assert.That(network.Created, Is.Empty, "no peer is ever created");
        });
    }

    [Test]
    public async Task Client_NetworkChange_RestartsIceOnTheOpenChannel()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        await using var a = Connect(f, "a", network);
        a.Transport.Start();
        await WaitUntil(() => a.Transport.IsDatagramActive);
        a.Transport.NetworkChanged();
        var participant = network.Created.Single(x => x.Role == RtcPeerRole.Offer);
        await WaitUntil(() => participant.IceRestarts == 1);
        await AwaitTransport<MediaTransportDescription>(f.Peers["a"], MediaTransportKind.Answer, x => x.IceRestart);
        Assert.That(a.Transport.IsDatagramActive, Is.True, "the channel stays up through the restart");
    }

    [Test]
    public async Task Client_NetworkChangesInQuickSuccession_RestartIceOneAtATime()
    {
        // Two offers before the first answer leave the browser applying an answer to an offer it has replaced:
        // each end then expects the other's previous ICE credentials and the restart can only fail.
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        await using var a = Connect(f, "a", network);
        a.Transport.Start();
        await WaitUntil(() => a.Transport.IsDatagramActive);
        var participant = network.Created.Single(x => x.Role == RtcPeerRole.Offer);
        network.AnswerDelay = TimeSpan.FromMilliseconds(500);
        for (var i = 0; i < 3; i++) a.Transport.NetworkChanged();
        await Task.Delay(250);
        Assert.That(participant.IceRestarts, Is.EqualTo(1), "one restart in flight at a time");
        await WaitUntil(() => participant.IceRestarts == 2, 4000);
        await Task.Delay(1200);
        Assert.Multiple(() =>
        {
            Assert.That(participant.IceRestarts, Is.EqualTo(2), "the changes during the first restart are folded into one more");
            Assert.That(a.Transport.IsDatagramActive, Is.True);
        });
    }

    [Test]
    public async Task Client_ARestartAnswerThatCannotBeApplied_MovesMediaToTheSocket_AndTriesAgain()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource(), x => x.Hysteresis = QuickPath));
        await using var a = Connect(f, "a", network, QuickClient);
        a.Transport.Start();
        await WaitUntil(() => a.Transport.IsDatagramActive);
        var participant = network.Created.Single(x => x.Role == RtcPeerRole.Offer);
        participant.FailRestartAnswer = true;
        a.Transport.NetworkChanged();
        // The browser refused the answer: its ICE credentials and the relay's no longer match, so nothing it sends
        // on that channel will arrive. Media must not keep going there.
        await WaitUntil(() => !a.Transport.IsDatagramActive);
        Assert.That(a.Transport.Status.Reason, Is.EqualTo("negotiation"));
        await WaitUntil(() => a.Transport.IsDatagramActive, 5000);
        Assert.That(network.Created.Count(x => x.Role == RtcPeerRole.Offer), Is.EqualTo(2), "a fresh session replaced the broken one");
    }

    [Test]
    public async Task Client_AChannelThatFlaps_IsLeftOnce_NotEveryBlip()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        await using var a = Connect(f, "a", network);
        a.Transport.Start();
        await WaitUntil(() => a.Transport.IsDatagramActive);
        var participant = network.Created.Single(x => x.Role == RtcPeerRole.Offer);
        var switches = 0;
        var last = true;
        var started = Environment.TickCount64;
        var tick = 0;
        while (Environment.TickCount64 - started < 4_000)
        {
            if (tick % 20 == 0) participant.Stall();
            if (tick % 20 == 6) participant.Recover();
            var now = a.Transport.IsDatagramActive;
            if (now != last) { switches++; last = now; }
            tick++;
            await Task.Delay(20);
        }
        var reports = f.Peers["a"].Received.Count(x => MediaTransportCodec.TryRead(x, out var kind, out _) && kind == MediaTransportKind.State);
        Assert.Multiple(() =>
        {
            Assert.That(switches, Is.LessThanOrEqualTo(2), "the path is left at the first blip and held off, not used again at every recovery");
            Assert.That(reports, Is.LessThanOrEqualTo(2), "and the relay is not told stalled/open at every blip");
        });
    }

    [Test]
    public async Task Client_AStalledChannel_SendsOnTheSocket_AndTellsTheRelay_UntilItRecovers()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource(), x => x.Hysteresis = QuickPath));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var stream = await f.Config("a");
        await using var a = Connect(f, "a", network, QuickClient);
        a.Transport.Start();
        await WaitUntil(() => a.Transport.IsDatagramActive);
        var audio = Frame(w => BoltCodec.WriteMediaFrame(w, stream, 1, 960, MediaFrameFlags.Encrypted, [1, 2, 3]));
        var participant = network.Created.Single(x => x.Role == RtcPeerRole.Offer);
        var relay = network.Created.Single(x => x.Role == RtcPeerRole.Answer);

        participant.Stall();
        await WaitUntil(() => !a.Transport.IsDatagramActive);
        Assert.Multiple(() =>
        {
            Assert.That(a.Transport.TrySend(audio), Is.False, "the caller sends on the socket");
            Assert.That(a.Transport.Status.Reason, Is.EqualTo("stalled"));
            Assert.That(participant.Disposed, Is.False, "the session is kept: ICE may come back by itself");
        });
        // The relay is told, so its media for this participant takes the socket too.
        await WaitUntil(() => Connection(f, "a").DatagramSuspended);
        await f.Send("b", await f.Config("b"));
        Assert.That(MediaOn(relay), Is.Zero);

        participant.Recover();
        await WaitUntil(() => a.Transport.IsDatagramActive);
        await WaitUntil(() => !Connection(f, "a").DatagramSuspended);
        Assert.That(a.Transport.TrySend(audio), Is.True);
    }

    [Test]
    public async Task Client_TellsTheRelayWhyItGaveUpOnAChannel()
    {
        var network = new FakeRtcNetwork { Reachable = false };
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        await using var a = Connect(f, "a", network);
        a.Transport.Start();
        await WaitUntil(() => a.Transport.Status.Reason == "timeout", 4000);
        await WaitUntil(() => f.Peers["a"].Received.Any(x => MediaTransportCodec.TryRead(x, out var k, out _) && k == MediaTransportKind.Close));
        var close = f.Peers["a"].Received.Select(x => MediaTransportCodec.TryRead(x, out var k, out var p) && k == MediaTransportKind.Close
            ? MediaTransportCodec.Decode<MediaTransportClose>(p.ToArray()) : null).First(x => x is not null)!;
        Assert.That(close.Reason, Is.EqualTo("timeout"), "the relay logs why a participant fell back to the WebSocket");
    }

    [Test]
    public async Task Client_RenewedCredentials_OpenABesideTheCurrentChannel_ThenReplaceIt()
    {
        var network = new FakeRtcNetwork();
        var ice = new FakeIceSource { Lifetime = TimeSpan.FromMinutes(2) };
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, ice));
        await using var a = Connect(f, "a", network);
        a.Transport.Start();
        await WaitUntil(() => a.Transport.IsDatagramActive);
        var first = a.Transport.ActivePeer;
        await WaitUntil(() => a.Transport.ActivePeer is { } peer && !ReferenceEquals(peer, first), 5000);
        await WaitUntil(() => ((FakeRtcPeer)first!).Disposed);
        Assert.That(network.Created.Where(x => x.Role == RtcPeerRole.Answer).Count(x => !x.Disposed), Is.EqualTo(1),
            "the relay keeps exactly the renewed peer");
    }

    [Test]
    public async Task Client_RelayReportedLoss_AddsRedundancyToItsAudio()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource()));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var stream = await f.Config("a");
        await using var a = Connect(f, "a", network);
        a.Transport.Start();
        await WaitUntil(() => a.Transport.IsDatagramActive);
        // 32 kbps Opus at 20 ms (80 bytes), plus a representative compact SFrame header/tag (20 bytes).
        byte[] Audio(uint sequence) => Frame(w => BoltCodec.WriteMediaFrame(w, stream, sequence, 960 * sequence, MediaFrameFlags.Encrypted, new byte[100]));
        // Every third datagram is lost on the way up; the relay measures that and tells the participant.
        network.DropEvery = 3;
        for (uint sequence = 1; sequence <= 30; sequence++) a.Transport.TrySend(Audio(sequence), audio: true);
        await WaitUntil(() => a.Transport.AudioRedundancy, 4000);
        network.DropEvery = 0;
        var participant = (FakeRtcPeer)a.Transport.ActivePeer!;
        a.Transport.TrySend(Audio(31), audio: true);
        a.Transport.TrySend(Audio(32), audio: true);
        // Stamped for this relay's transport feedback; inside the stamp, the previous frame rides along.
        Assert.That(TransportSequenceCodec.TryRead(participant.Sent.Last(), out _, out var last), Is.True);
        Assert.That(last[0], Is.EqualTo((byte)FrameType.MediaBundle), "the previous frame rides along");
        Assert.That(last.Length, Is.EqualTo(MediaBundleCodec.Size(Audio(31).Length, Audio(32).Length)));
        var beforeRedundancyKbps = Audio(32).Length * 8 * 50 / 1000;
        var decision = new Bolt.Media.Congestion.SendRateController(512).Update(new Bolt.Media.Congestion.SendPathSample(
            0, 500, beforeRedundancyKbps, 5, 0, false, AudioRedundancy: a.Transport.AudioRedundancy));
        Assert.That(decision.VideoKbps, Is.EqualTo(373), "the pacer's 52 kbps excludes the repeated frame the negotiated transport just bundled");
        await WaitUntil(() => f.Peers["b"].Media(stream).Any(x => x.Sequence == 32));
        Assert.That(f.Peers["b"].Media(stream).GroupBy(x => x.Sequence).All(x => x.Count() == 1), Is.True, "every frame reaches b once");
        Assert.That(a.Transport.Status.AudioRedundancy, Is.True);
    }

    [Test]
    public async Task Client_DatagramMessages_ReachTheMediaHandlers_ButOnlyMediaDoes()
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync();
        await using var a = Connect(f, "a", network);
        var seen = new List<FrameType>();
        a.Client.RegisterFrameHandler(FrameType.MediaFrame, (_, buffer, _) => seen.Add((FrameType)buffer[0]));
        a.Client.RegisterFrameHandler(FrameType.CallSignal, (_, buffer, _) => seen.Add((FrameType)buffer[0]));
        a.Client.RegisterFrameHandler(FrameType.MediaConfig, (_, buffer, _) => seen.Add((FrameType)buffer[0]));
        var media = Frame(w => BoltCodec.WriteMediaFrame(w, Guid.NewGuid(), 1, 960, 0, [1]));
        var bundle = new byte[MediaBundleCodec.Size(media.Length, media.Length)];
        MediaBundleCodec.Write(bundle, media, media);
        Assert.Multiple(() =>
        {
            Assert.That(a.Client.DispatchDatagram(media), Is.EqualTo(1));
            Assert.That(a.Client.DispatchDatagram(bundle), Is.EqualTo(2));
            Assert.That(a.Client.DispatchDatagram(Frame(w => BoltCodec.WriteCallSignal(w, Guid.NewGuid(), SignalType.End, []))), Is.Zero,
                "a signal can only come from the authenticated socket");
            Assert.That(a.Client.DispatchDatagram(Frame(w => BoltCodec.WriteMediaConfig(w, Guid.NewGuid(), Guid.NewGuid(), MediaType.Audio, CodecId.Opus, 48000, 1, 32, 0x10, []))), Is.Zero,
                "nor can a stream configuration");
            Assert.That(a.Client.DispatchDatagram(bundle.AsSpan(0, bundle.Length - 1)), Is.Zero, "a truncated bundle is dropped whole");
            Assert.That(seen, Is.EqualTo(new[] { FrameType.MediaFrame, FrameType.MediaFrame, FrameType.MediaFrame }));
        });
    }
}

public sealed partial class BoltGroupCallLifecycleTests
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task Client_StampsWhatItSends_OnlyForARelayThatReports_AndHearsHowItsUplinkDid(bool relayReports)
    {
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o =>
        {
            var transport = Transport(network, new FakeIceSource(), x => x.Hysteresis = QuickPath);
            o.MediaTransport = new Bolt.Server.BoltMediaTransportOptions
            {
                Peers = transport.Peers, IceServers = transport.IceServers, RequestSpacingSeconds = 0, PathHysteresis = QuickPath,
                TransportFeedback = relayReports,
            };
        });
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var stream = await f.Config("a");
        await using var a = Connect(f, "a", network, QuickClient);
        Bolt.Media.Congestion.TransportSignal? heard = null;
        a.Transport.TransportFeedback += signal => heard = signal;
        a.Transport.Start();
        await WaitUntil(() => a.Transport.IsDatagramActive);
        Assert.That(a.Transport.StampsMessages, Is.EqualTo(relayReports));

        for (uint sequence = 1; sequence <= 20; sequence++)
            Assert.That(a.Transport.TrySend(Frame(w => BoltCodec.WriteMediaFrame(w, stream, sequence, 960 * sequence, MediaFrameFlags.Encrypted, new byte[100])), audio: true), Is.True);
        await WaitUntil(() => f.Peers["b"].Media(stream).Count == 20);

        var participant = network.Created.Single(x => x.Role == Bolt.Protocol.Transport.RtcPeerRole.Offer);
        Assert.That(participant.Sent.All(x => x[0] == (byte)(relayReports ? FrameType.TransportSequenced : FrameType.MediaFrame)), Is.True);
        if (!relayReports)
        {
            await Task.Delay(300);
            Assert.That(heard, Is.Null, "an older relay never reports, and is never sent a stamp it would refuse");
            return;
        }
        await WaitUntil(() => heard is { } s && s.LossFraction == 0, 2_000);
        Assert.That(Connection(f, "a").DatagramRejected, Is.Zero, "every stamped message was unwrapped and taken");
    }
    [TestCase(500, false)]
    [TestCase(1_000, false)]
    [TestCase(500, true)]
    [TestCase(1_000, true)]
    public async Task Client_RepairsUplinkLoss_WhenFeedbackTakesALongRoundTrip(int rttMs, bool paced)
    {
        var network = new FakeRtcNetwork { Path = new("relay", "udp", "udp", "relay", rttMs) };
        await using var f = await Fixture.CreateAsync(configure: o => o.MediaTransport = Transport(network, new FakeIceSource(), x => x.Hysteresis = QuickPath));
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var video = await f.VideoConfig("a");
        var now = Environment.TickCount64;
        await using var a = Connect(f, "a", network, QuickClient, () => now);
        a.Transport.Start();
        await WaitUntil(() => a.Transport.IsDatagramActive && a.Transport.StampsMessages);
        var sender = network.Created.Single(x => x.Role == RtcPeerRole.Offer);
        sender.Partner!.LoseSent = message => message[0] == (byte)FrameType.TransportFeedback;
        var copies = 0;
        sender.LoseSent = message => TransportSequenceCodec.TryRead(message, out _, out var inner) &&
            BoltCodec.TryReadMediaFrame(inner, out var media) && media.SequenceNumber == 1 && Interlocked.Increment(ref copies) == 1;
        // An earlier arrival anchors the relay's transport sequence; the later arrival reveals the lost packet.
        a.Transport.TrySend(Frame(w => BoltCodec.WriteMediaFrame(w, video, 0, 0,
            (byte)(MediaFrameFlags.Keyframe | MediaFrameFlags.Encrypted), new byte[800])));
        var frame = Frame(w => BoltCodec.WriteMediaFrame(w, video, 1, 3000, (byte)(MediaFrameFlags.Keyframe | MediaFrameFlags.Encrypted), new byte[800]));
        Assert.That(a.Transport.TrySend(frame), Is.True);
        var sent = sender.Sent.Last();
        Assert.That(TransportSequenceCodec.TryRead(sent, out var transportSequence, out _), Is.True);
        a.Transport.TrySend(Frame(w => BoltCodec.WriteMediaFrame(w, video, 2, 6000, MediaFrameFlags.Encrypted, new byte[800])));

        var order = new System.Collections.Concurrent.ConcurrentQueue<string>();
        await using var pacer = new Bolt.Media.Congestion.MediaSendPacer((_, _) =>
        {
            order.Enqueue("audio");
            return ValueTask.CompletedTask;
        }, () => sender.BufferedAmount, clock: () => now);
        if (paced)
        {
            sender.BufferedAmount = 100_000;
            a.Transport.QueueVideoRepair = (repair, firstSentAt, expiresAt) => pacer.EnqueueVideoRepair(repair, expiresAt, (bytes, _) =>
            {
                Assert.That(a.Transport.TrySendVideoRepair(bytes.Span, firstSentAt), Is.True);
                order.Enqueue("repair");
                return ValueTask.CompletedTask;
            });
            pacer.Start();
        }

        now += rttMs + Bolt.Server.BoltServer.TransportFeedbackIntervalMs;
        sender.Deliver(TransportFeedbackCodec.Write(transportSequence, [-1L, Bolt.Server.TransportFeedbackRecorder.NowMicroseconds()]));
        if (paced)
        {
            Assert.That(a.Transport.UplinkResent, Is.Zero, "a delayed loss report cannot bypass a full channel");
            pacer.EnqueueAudio([9]);
            sender.Drain(0);
            await WaitUntil(() => order.Count == 2);
            Assert.That(order, Is.EqualTo(new[] { "audio", "repair" }), "voice overtakes the queued repair");
        }
        Assert.That(a.Transport.UplinkResent, Is.EqualTo(1), "the first feedback took one round trip plus the relay's report interval");
        await WaitUntil(() => f.Peers["b"].Media(video).Any(x => x.Sequence == 1));

        // Losing a repair does not make the original picture young again, even if its RTT would otherwise permit it.
        Assert.That(TransportSequenceCodec.TryRead(sender.Sent.Last(), out var repairedSequence, out _), Is.True);
        now += 301;
        sender.Deliver(TransportFeedbackCodec.Write(repairedSequence, [-1L]));
        Assert.That(a.Transport.UplinkResent, Is.EqualTo(1), "repeat loss keeps the first send's deadline");
    }

    [Test]
    [CancelAfter(30_000)]
    public async Task Client_ResendsTheVideoItsUplinkLost_AsSoonAsTheRelayReportsIt()
    {
        // Production 2026-10-04 13:08 UTC: a fragment the phone's uplink lost could only come back from the phone, after
        // a receiver noticed, asked the relay, and the relay asked the phone: longer than the receiver waited. Every
        // keyframe of a large picture lost a fragment that way, so none was ever shown. The relay's transport feedback
        // already says within 100 ms what never arrived: the sender sends that video again then, unasked.
        var network = new FakeRtcNetwork();
        await using var f = await Fixture.CreateAsync(configure: o =>
        {
            var transport = Transport(network, new FakeIceSource(), x => x.Hysteresis = QuickPath);
            o.MediaTransport = new Bolt.Server.BoltMediaTransportOptions
            {
                Peers = transport.Peers, IceServers = transport.IceServers, RequestSpacingSeconds = 0, PathHysteresis = QuickPath,
            };
        });
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var video = await f.VideoConfig("a");
        await using var a = Connect(f, "a", network, QuickClient);
        a.Transport.Start();
        await WaitUntil(() => a.Transport.IsDatagramActive && a.Transport.StampsMessages);
        // The uplink loses the first copy of every fifth video frame, and of frame 10 the second copy too.
        var copies = new Dictionary<uint, int>();
        network.Created.Single(x => x.Role == Bolt.Protocol.Transport.RtcPeerRole.Offer).LoseSent = message =>
        {
            if (!TransportSequenceCodec.TryRead(message, out _, out var inner) || !BoltCodec.TryReadMediaFrame(inner, out var media)) return false;
            var copy = copies[media.SequenceNumber] = copies.GetValueOrDefault(media.SequenceNumber) + 1;
            return media.SequenceNumber % 5 == 0 && (copy == 1 || (copy == 2 && media.SequenceNumber == 10));
        };

        // One picture of 40 fragments (a keyframe), then single-fragment pictures.
        uint sequence = 0;
        for (var fragment = 0; fragment < 40; fragment++)
            a.Transport.TrySend(Frame(w => BoltCodec.WriteMediaFrame(w, video, ++sequence, 3000,
                (byte)(fragment == 0 ? MediaFrameFlags.Keyframe | MediaFrameFlags.Encrypted : MediaFrameFlags.Encrypted), new byte[800])));
        for (var picture = 2; picture <= 20; picture++)
            a.Transport.TrySend(Frame(w => BoltCodec.WriteMediaFrame(w, video, ++sequence, 3000u * (uint)picture, MediaFrameFlags.Encrypted, new byte[800])));

        var expected = Enumerable.Range(1, (int)sequence).Select(x => (uint)x).ToArray();
        await WaitUntil(() => f.Peers["b"].Media(video).Select(x => x.Sequence).Distinct().Count() == expected.Length, 3_000);
        Assert.That(f.Peers["b"].Media(video).Select(x => x.Sequence).Distinct().Order(), Is.EqualTo(expected),
            "every fragment reached the receiver, the lost ones sent again by the phone");
        Assert.That(a.Transport.UplinkResent, Is.EqualTo(sequence / 5 + 1), "frame 10 twice: its first copy sent again was lost too");
    }
}

