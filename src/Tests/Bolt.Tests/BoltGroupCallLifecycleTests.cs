using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Channels;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Bolt.Server;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

public sealed class BoltGroupCallLifecycleTests
{
    [Test]
    public async Task CanceledFirstAdmission_ReleasesEmptyActiveRoom()
    {
        await using var f = await Fixture.CreateAsync();
        using var canceled = new CancellationTokenSource();
        f.Policy.Accepted.Add("a");
        f.Policy.OnAuthorize = () => canceled.Cancel();
        Assert.CatchAsync<OperationCanceledException>(() => f.Server.JoinGroupCallAsync(f.Call, "a", canceled.Token));
        var calls = (ConcurrentDictionary<Guid, ServerCallState>)typeof(BoltServer).GetField("_activeCalls", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Server)!;
        Assert.That(calls, Is.Empty);
        f.Policy.OnAuthorize = null;
        Assert.That(await f.Join("a"), Is.True);
    }

    [Test]
    public async Task ConfigFanout_ClosedReceiverQueue_DoesNotDisconnectHealthySender()
    {
        await using var f = await Fixture.CreateAsync();
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var connections = (ConcurrentDictionary<string, BoltHubConnection>)typeof(BoltServer)
            .GetField("_connectionsByStreamId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Server)!;
        connections.Values.Single(x => x.ClientId == "b").CompleteSendChannel();
        var stream = await f.Config("a");
        await f.Send("a", stream);
        Assert.Multiple(() =>
        {
            Assert.That(f.Tasks["a"].IsCompleted, Is.False);
            Assert.That(f.Peers["c"].Count(FrameType.MediaConfig), Is.EqualTo(1));
            Assert.That(f.Peers["c"].Count(FrameType.MediaFrame), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GroupLimits_AllowEightMembersAndOneAudioStreamEach()
    {
        await using var f = await Fixture.CreateAsync(limit: 32, participants: 9);
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys.Take(8)) Assert.That(await f.Join(id), Is.True);
        Assert.That(await f.Join("i"), Is.False);
        await f.Config("a");
        await f.Config("a");
        Assert.That(f.Peers["b"].Count(FrameType.MediaConfig), Is.EqualTo(1));
    }

    [Test]
    public async Task Admission_WithoutGroupPolicy_IsDisabled()
    {
        await using var fixture = await Fixture.CreateAsync(policyEnabled: false);
        Assert.That(await fixture.Server.JoinGroupCallAsync(fixture.Call, "a"), Is.False);
    }

    [Test]
    public async Task UnacceptedParticipant_CannotJoinPublishOrReceiveAudio()
    {
        await using var f = await Fixture.CreateAsync();
        f.Policy.Accepted.UnionWith(["a", "b"]);
        Assert.That(await f.Join("a"), Is.True);
        Assert.That(await f.Join("b"), Is.True);
        Assert.That(await f.Join("c"), Is.False);
        var stream = await f.Config("a");
        await f.Send("a", stream);
        await f.Config("c");
        Assert.Multiple(() =>
        {
            Assert.That(f.Peers["b"].Count(FrameType.MediaFrame), Is.EqualTo(1));
            Assert.That(f.Peers["c"].Count(FrameType.MediaFrame), Is.Zero);
            Assert.That(f.Peers["c"].Count(FrameType.MediaConfig), Is.Zero);
            Assert.That(f.Peers["a"].Count(FrameType.MediaConfig), Is.Zero);
        });
    }

    [Test]
    public async Task LateJoin_ReceivesConfigurationBeforeLiveAudio()
    {
        await using var f = await Fixture.CreateAsync();
        f.Policy.Accepted.UnionWith(["a", "b", "c"]);
        await f.Join("a"); await f.Join("b");
        var stream = await f.Config("a");
        Assert.That(await f.Join("c"), Is.True);
        await f.Send("a", stream);
        Assert.That(f.Peers["c"].Sent.Where(x => x[0] != (byte)FrameType.RegisterAck).Select(x => (FrameType)x[0]),
            Is.EqualTo(new[] { FrameType.MediaConfig, FrameType.MediaFrame }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LeaveOrDisconnect_RemovesOnlyThatMemberAndItsStreams(bool disconnect)
    {
        await using var f = await Fixture.CreateAsync();
        f.Policy.Accepted.UnionWith(["a", "b", "c"]);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var aStream = await f.Config("a");
        var cStream = await f.Config("c");
        await f.Send("a", aStream);
        // Processing the sender only queues delivery; observe it before disconnect drops pending frames.
        Assert.That(() => f.Peers["c"].Count(FrameType.MediaFrame), Is.EqualTo(1).After(3000, 10));
        if (disconnect)
        {
            await f.Peers["c"].DisposeAsync();
            await f.Tasks["c"].WaitAsync(TimeSpan.FromSeconds(3));
        }
        else await f.Peers["c"].ProcessAsync(Frame(w => BoltCodec.WriteCallSignal(w, f.Call, SignalType.End, [])));
        await f.Send("a", aStream);
        if (!disconnect) await f.Send("c", cStream);
        Assert.Multiple(() =>
        {
            Assert.That(f.Peers["b"].Count(FrameType.MediaFrame), Is.EqualTo(2));
            Assert.That(f.Peers["c"].Count(FrameType.MediaFrame), Is.EqualTo(1));
            Assert.That(f.Peers["a"].Signals(SignalType.StreamEnded), Is.EqualTo(1));
            Assert.That(f.Peers["b"].Signals(SignalType.End), Is.Zero);
        });
    }

    [TestCase("c")]
    [TestCase("a")]
    public async Task AuthorizationLease_RechecksEveryParticipant_AndRemovesRevokedMember(string revoked)
    {
        await using var f = await Fixture.CreateAsync();
        f.Policy.Accepted.UnionWith(["a", "b", "c"]);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var stream = await f.Config("a");
        f.Policy.Accepted.Remove(revoked);
        var calls = (ConcurrentDictionary<Guid, ServerCallState>)typeof(BoltServer).GetField("_activeCalls", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Server)!;
        calls[f.Call].LastMediaAuthorizationTick = Environment.TickCount64 - 5001;
        // The due lease starts a background re-check; the frame that noticed it is not held back.
        await f.Send("a", stream);
        Assert.That(() => calls[f.Call].Participants.Select(x => x.ClientId).ToArray(),
            Is.EquivalentTo(f.Peers.Keys.Where(x => x != revoked)).After(3000, 10));
        var before = f.Peers.ToDictionary(x => x.Key, x => x.Value.Count(FrameType.MediaFrame));
        await f.Send("a", stream);
        Assert.Multiple(() =>
        {
            // Once the refusal is applied the revoked member neither sends nor receives.
            Assert.That(() => f.Peers["b"].Count(FrameType.MediaFrame) - before["b"], Is.EqualTo(revoked == "a" ? 0 : 1).After(1000, 10));
            Assert.That(f.Peers["c"].Count(FrameType.MediaFrame) - before["c"], Is.Zero);
        });
    }

    [Test]
    public async Task Admission_ParticipantBound_AndDifferentCallBinding_AreEnforced()
    {
        await using var f = await Fixture.CreateAsync(limit: 2);
        f.Policy.Accepted.UnionWith(["a", "b", "c"]);
        Assert.That(await f.Join("a"), Is.True);
        Assert.That(await f.Join("b"), Is.True);
        Assert.That(await f.Join("c"), Is.False);
        Assert.That(await f.Server.JoinGroupCallAsync(Guid.NewGuid(), "c"), Is.False);
        await f.Server.LeaveGroupCallAsync(f.Call, "b");
        Assert.That(await f.Join("c"), Is.True);
    }

    // ── Slow and stalled receivers ──

    [Test]
    public async Task SlowReceiver_DoesNotStallTheSenderOrOtherReceivers()
    {
        // Yap's control-queue timeout, with the progress watchdog far away.
        await using var f = await Fixture.CreateAsync(configure: o => { o.SendEnqueueTimeoutMs = 50; o.TransportSendStallTimeoutMs = 10_000; });
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var stream = await f.Config("a");
        Assert.That(() => f.Peers["c"].Count(FrameType.MediaConfig), Is.EqualTo(1).After(3000, 10));
        f.Peers["c"].Block();
        var started = Stopwatch.StartNew();
        for (var i = 0; i < 20; i++) await f.Send("a", stream);
        Assert.Multiple(() =>
        {
            Assert.That(started.ElapsedMilliseconds, Is.LessThan(1000), "the sender's receive loop never waits for a receiver");
            Assert.That(() => f.Peers["b"].Media(stream), Has.Count.EqualTo(20).After(3000, 10));
            Assert.That(f.Tasks["a"].IsCompleted, Is.False);
        });
        await Task.Delay(300); // Far beyond the old 50/250 ms deadline that retired a receiver.
        Assert.That(f.Participants(), Does.Contain("c"));
        f.Peers["c"].Release();
        Assert.That(() => f.Peers["c"].Media(stream).Select(x => x.Sequence), Does.Contain(20u).After(3000, 10));
    }

    [Test]
    public async Task StalledReceiver_IsRetiredOnlyAfterTheStallWindow()
    {
        await using var f = await Fixture.CreateAsync(configure: o => { o.SendEnqueueTimeoutMs = 50; o.TransportSendStallTimeoutMs = 1_500; });
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var stream = await f.Config("a");
        Assert.That(() => f.Peers["c"].Count(FrameType.MediaConfig), Is.EqualTo(1).After(3000, 10));
        f.Peers["c"].Block();
        var stalled = Stopwatch.StartNew();
        await f.Send("a", stream);
        await Task.Delay(700);
        Assert.That(f.Participants(), Does.Contain("c"), "a write that is merely slow is not a dead link");
        Assert.That(() => f.Tasks["c"].IsCompleted, Is.True.After(5000, 20));
        Assert.That(stalled.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(1400));
        Assert.That(f.Participants(), Is.EquivalentTo(new[] { "a", "b" }));
    }

    [Test]
    public async Task CongestedReceiver_KeepsAudio_DropsWholePicturesUntilAKeyframe_AndAsksTheSender()
    {
        await using var f = await Fixture.CreateAsync(configure: o =>
        {
            o.TransportSendStallTimeoutMs = 10_000;
            o.MediaSendQueue.VideoMaxQueuedBytes = 3_000;
            o.MediaSendQueue.VideoMaxQueueDelayMs = 60_000;
            o.MediaSendQueue.AudioMaxQueueDelayMs = 60_000;
        });
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var audio = await f.Config("a");
        var video = await f.VideoConfig("a");
        await f.SendVideo("a", video, keyframe: true);
        Assert.That(() => f.Peers["c"].Media(video), Has.Count.EqualTo(1).After(3000, 10));

        f.Peers["c"].Block();
        await f.SendVideo("a", video, keyframe: false); // 2: taken by the blocked write
        Assert.That(() => f.Peers["b"].Media(video), Has.Count.EqualTo(2).After(3000, 10));
        await Task.Delay(50);
        await f.SendVideo("a", video, keyframe: false); // 3: queued
        await f.SendVideo("a", video, keyframe: false); // 4: queued
        await f.SendVideo("a", video, keyframe: false); // 5: over budget - 3, 4 and 5 go
        for (var i = 0; i < 3; i++) await f.Send("a", audio);
        await f.SendVideo("a", video, keyframe: false); // 6: undecodable without 3-5
        Assert.That(() => f.Peers["a"].KeyRequests(video), Is.GreaterThanOrEqualTo(1).After(3000, 10),
            "the relay asks the sender for a fresh reference picture");

        f.Peers["c"].Release();
        await f.SendVideo("a", video, keyframe: true);  // 7
        await f.SendVideo("a", video, keyframe: false); // 8
        Assert.That(() => f.Peers["c"].Media(video).Select(x => x.Sequence), Is.EqualTo(new uint[] { 1, 2, 7, 8 }).After(3000, 10));
        Assert.That(() => f.Peers["c"].Media(audio), Has.Count.EqualTo(3).After(3000, 10), "audio is never dropped for video");
        Assert.That(() => f.Peers["b"].Media(video), Has.Count.EqualTo(8).After(3000, 10), "a healthy receiver loses nothing");
    }

    [Test]
    public async Task LateVideoReceiver_WaitsForAKeyframe_AndTheRelayRequestsOne()
    {
        await using var f = await Fixture.CreateAsync();
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        await f.Join("a"); await f.Join("b");
        var video = await f.VideoConfig("a");
        await f.SendVideo("a", video, keyframe: true);
        await f.SendVideo("a", video, keyframe: false);
        Assert.That(await f.Join("c"), Is.True);
        await f.SendVideo("a", video, keyframe: false);
        Assert.That(() => f.Peers["a"].KeyRequests(video), Is.EqualTo(1).After(3000, 10));
        await f.SendVideo("a", video, keyframe: true);
        await f.SendVideo("a", video, keyframe: false);
        Assert.That(() => f.Peers["c"].Media(video).Select(x => x.Keyframe), Is.EqualTo(new[] { true, false }).After(3000, 10),
            "a decoder must never be fed a delta it has no reference for");
        Assert.That(() => f.Peers["b"].Media(video), Has.Count.EqualTo(5).After(3000, 10));
    }

    // ── Phase 1: congestion reports and temporal layers ──

    [Test]
    public async Task Sender_GetsTheRelaysCongestionReports_AndNoParticipantCanForgeOne()
    {
        await using var f = await Fixture.CreateAsync();
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var video = await f.VideoConfig("a");
        await f.SendVideo("a", video, keyframe: true);
        Assert.That(() => f.Peers["a"].Congestion(video), Is.Not.Empty.After(3000, 10), "the first picture brings a report");
        Assert.That(f.Peers["a"].Congestion(video)[0].Receivers, Is.EqualTo(2));
        Assert.That(f.Peers["b"].Congestion(video), Is.Empty, "reports go to the sender only");

        // b claims to be the relay and reports an absurd queue on a's stream. On a media-only relay a frame type
        // a participant may not send closes its connection, like any other.
        f.Peers["b"].Send(Frame(w => BoltCodec.WriteMediaCongestion(w,
            new MediaCongestionData { StreamId = video, QueueDelayMs = 9_999, AllowedKbps = 1 })));
        await f.Tasks["b"].WaitAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(300);
        await f.SendVideo("a", video, keyframe: false);
        Assert.That(() => f.Peers["a"].Congestion(video), Has.Count.GreaterThanOrEqualTo(2).After(3000, 10));
        Assert.That(f.Peers["a"].Congestion(video).Any(x => x.QueueDelayMs == 9_999), Is.False,
            "only the relay originates congestion reports");
    }

    [Test]
    public async Task CongestedReceiver_LosesEnhancementPictures_KeepsTheBaseLayer_AndNeedsNoKeyframe()
    {
        await using var f = await Fixture.CreateAsync();
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var video = await f.VideoConfig("a");
        await f.SendLayered("a", video, 0, keyframe: true); // 1
        Assert.That(() => f.Peers["c"].Media(video), Has.Count.EqualTo(1).After(3000, 10));

        f.Peers["c"].Block();
        await f.SendLayered("a", video, 2); // 2: taken by the blocked write
        Assert.That(() => f.Peers["b"].Media(video), Has.Count.EqualTo(2).After(3000, 10));
        await Task.Delay(50);
        await f.SendLayered("a", video, 1); // 3: queued
        await Task.Delay(700);              // c's oldest queued picture is now past both shedding thresholds
        await f.SendLayered("a", video, 2); // 4: shed
        await f.SendLayered("a", video, 0); // 5: the base layer always goes
        await f.SendLayered("a", video, 1); // 6: shed
        f.Peers["c"].Release();
        Assert.That(() => f.Peers["c"].Media(video).Select(x => x.Sequence), Is.EqualTo(new uint[] { 1, 2, 3, 5 }).After(3000, 10));
        await f.SendLayered("a", video, 0); // 7: the queue drained; layers come back at a base picture
        await f.SendLayered("a", video, 2); // 8
        Assert.That(() => f.Peers["c"].Media(video).Select(x => x.Sequence), Is.EqualTo(new uint[] { 1, 2, 3, 5, 7, 8 }).After(3000, 10));
        Assert.That(f.Peers["a"].KeyRequests(video), Is.Zero, "no picture the receiver kept refers to one it lost");
        Assert.That(() => f.Peers["b"].Media(video), Has.Count.EqualTo(8).After(3000, 10), "a healthy receiver loses nothing");
    }

    // ── Re-authorization ──

    [Test]
    public async Task UnreachablePolicy_KeepsTheParticipant_UntilADefinitiveRefusal()
    {
        await using var f = await Fixture.CreateAsync();
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var stream = await f.Config("a");
        f.Policy.Unreachable.UnionWith(f.Peers.Keys);
        f.ExpireLease();
        await f.Send("a", stream);
        Assert.That(() => f.RenewalIdle, Is.True.After(3000, 10));
        await f.Send("a", stream);
        Assert.Multiple(() =>
        {
            Assert.That(f.Participants(), Is.EquivalentTo(f.Peers.Keys), "a hub reconnect must not end the call");
            Assert.That(() => f.Peers["c"].Media(stream), Has.Count.EqualTo(2).After(3000, 10));
        });

        f.Policy.Unreachable.Clear();
        f.Policy.Accepted.Remove("c");
        f.ExpireLease();
        await f.Send("a", stream);
        Assert.That(() => f.Participants(), Is.EquivalentTo(new[] { "a", "b" }).After(3000, 10), "a refusal still removes");
    }

    [Test]
    public async Task UnreachablePolicy_BeyondTheGrace_RemovesOnlyThatParticipant()
    {
        await using var f = await Fixture.CreateAsync(configure: o => o.GroupAuthorizationGraceSeconds = 0);
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var stream = await f.Config("a");
        f.Policy.Unreachable.Add("c");
        f.ExpireLease();
        await f.Send("a", stream);
        Assert.That(() => f.Participants(), Is.EquivalentTo(new[] { "a", "b" }).After(3000, 10));
    }

    [Test]
    public async Task SlowAuthorizationRenewal_DoesNotHoldBackMedia()
    {
        await using var f = await Fixture.CreateAsync();
        f.Policy.Accepted.UnionWith(f.Peers.Keys);
        foreach (var id in f.Peers.Keys) await f.Join(id);
        var stream = await f.Config("a");
        f.Policy.DelayMs = 2_000;
        f.ExpireLease();
        var sent = Stopwatch.StartNew();
        await f.Send("a", stream);
        await f.Send("a", stream);
        Assert.That(() => f.Peers["b"].Media(stream), Has.Count.EqualTo(2).After(1000, 5));
        Assert.That(sent.ElapsedMilliseconds, Is.LessThan(1500), "four policy RPCs per renewal used to stall every frame of the call");
        Assert.That(() => f.RenewalIdle, Is.True.After(5000, 20));
        Assert.That(f.Participants(), Is.EquivalentTo(f.Peers.Keys));
    }

    private static byte[] Frame(Action<IBufferWriter<byte>> write)
    { var writer = new ArrayBufferWriter<byte>(); write(writer); return writer.WrittenSpan.ToArray(); }

    private sealed class Policy(Guid call) : IBoltCallAuthorizer, IBoltGroupCallAuthorizer
    {
        public HashSet<string> Accepted { get; } = [];
        /// <summary>Participants whose check throws, like a policy RPC over a hub link that is reconnecting.</summary>
        public HashSet<string> Unreachable { get; } = [];
        public Action? OnAuthorize;
        public int DelayMs;
        public ValueTask<bool> AuthorizeAsync(BoltCallAuthorizationContext context, CancellationToken ct = default) => ValueTask.FromResult(false);
        public async ValueTask<bool> AuthorizeParticipantAsync(Guid id, string clientId, ClaimsPrincipal user, CancellationToken ct = default)
        {
            OnAuthorize?.Invoke();
            if (DelayMs > 0) await Task.Delay(DelayMs, ct);
            if (Unreachable.Contains(clientId)) throw new IOException("The policy backend is unreachable.");
            return id == call && Accepted.Contains(clientId) && user.FindFirstValue("bolt_media_client_id") == clientId;
        }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Call { get; } = Guid.NewGuid();
        public Policy Policy { get; private set; } = null!;
        public BoltServer Server { get; private set; } = null!;
        public Dictionary<string, Peer> Peers { get; } = [];
        public Dictionary<string, Task> Tasks { get; } = [];
        public static async Task<Fixture> CreateAsync(bool policyEnabled = true, int limit = 8, int participants = 3,
            Action<BoltServerOptions>? configure = null)
        {
            var f = new Fixture(); f.Policy = new(f.Call);
            var options = new BoltServerOptions
            { MediaEnabled = true, AuthenticatedMediaOnly = true, RequireSecureTransport = true, CallAuthorizer = f.Policy,
                GroupCallAuthorizer = policyEnabled ? f.Policy : null, MaxCallParticipants = limit };
            configure?.Invoke(options);
            f.Server = new(NullLogger<BoltServer>.Instance, options);
            foreach (var id in Enumerable.Range(0, participants).Select(index => ((char)('a' + index)).ToString()))
            {
                var peer = new Peer(); f.Peers[id] = peer;
                var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", id), new Claim("bolt_media_client_id", id)], "fixture"));
                f.Tasks[id] = f.Server.HandleConnectionAsync(peer, principal, CancellationToken.None, isSecureTransport: true);
                await peer.ProcessAsync(Frame(w => BoltCodec.WriteRegister(w, id, id)));
            }
            return f;
        }
        public Task<bool> Join(string id) => Server.JoinGroupCallAsync(Call, id);
        public async Task<Guid> Config(string id)
        {
            var stream = Guid.NewGuid();
            await Peers[id].ProcessAsync(Frame(w => BoltCodec.WriteMediaConfig(w, stream, Call, MediaType.Audio, CodecId.Opus, 48000, 1, 128, 0, [])));
            return stream;
        }
        private readonly Dictionary<Guid, uint> sequences = [];
        // Real senders number every frame; the relay treats a repeated number as a stale retransmission.
        public Task Send(string id, Guid stream)
        {
            var sequence = sequences[stream] = sequences.GetValueOrDefault(stream) + 1;
            return Peers[id].ProcessAsync(Frame(w => BoltCodec.WriteMediaFrame(w, stream, sequence, 960 * sequence, 0, [0xF8, 0xFF, 0xFE])));
        }
        public async Task<Guid> VideoConfig(string id)
        {
            var stream = Guid.NewGuid();
            await Peers[id].ProcessAsync(Frame(w => BoltCodec.WriteMediaConfig(w, stream, Call, MediaType.Video, CodecId.H264, 426, 240, 180, 0, [])));
            return stream;
        }
        /// <summary>One single-fragment picture of a temporal layer, marked in the clear flags as the browser sender does.</summary>
        public Task SendLayered(string id, Guid stream, int layer, bool keyframe = false, int bytes = 1000)
        {
            var sequence = sequences[stream] = sequences.GetValueOrDefault(stream) + 1;
            var flags = MediaFrameFlags.WithTemporalLayer(keyframe ? MediaFrameFlags.Keyframe : (byte)0, layer);
            return Peers[id].ProcessAsync(Frame(w => BoltCodec.WriteMediaFrame(w, stream, sequence, 3000 * sequence, flags, new byte[bytes])));
        }
        /// <summary>One single-fragment picture; a keyframe carries the clear keyframe flag like a real first fragment.</summary>
        public Task SendVideo(string id, Guid stream, bool keyframe, int bytes = 1000)
        {
            var sequence = sequences[stream] = sequences.GetValueOrDefault(stream) + 1;
            return Peers[id].ProcessAsync(Frame(w => BoltCodec.WriteMediaFrame(w, stream, sequence, 3000 * sequence, keyframe ? (byte)0x01 : (byte)0, new byte[bytes])));
        }
        public ServerCallState CallState => ((ConcurrentDictionary<Guid, ServerCallState>)typeof(BoltServer)
            .GetField("_activeCalls", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Server)!)[Call];
        public string[] Participants() { lock (CallState.Participants) return CallState.Participants.Select(x => x.ClientId!).ToArray(); }
        /// <summary>Make the next media frame find the authorization lease due.</summary>
        public void ExpireLease() => Volatile.Write(ref CallState.LastMediaAuthorizationTick, Environment.TickCount64 - 60_000);
        public bool RenewalIdle => Volatile.Read(ref CallState.AuthorizationRenewalRunning) == 0 &&
            Environment.TickCount64 - Volatile.Read(ref CallState.LastMediaAuthorizationTick) < 30_000;
        public async ValueTask DisposeAsync()
        {
            foreach (var peer in Peers.Values) { peer.Release(); await peer.DisposeAsync(); }
            await Task.WhenAll(Tasks.Values).WaitAsync(TimeSpan.FromSeconds(5)); Server.Dispose();
        }
    }
    private sealed class Peer : IBoltConnection
    {
        private readonly Channel<(byte[]? Frame, TaskCompletionSource? Barrier)> inbound = Channel.CreateUnbounded<(byte[]?, TaskCompletionSource?)>();
        public ConcurrentQueue<byte[]> Sent { get; } = new();
        public bool IsConnected { get; private set; } = true;
        public bool SupportsDatagrams => false;
        public BoltTransport TransportType => BoltTransport.WebSocket;
        public int Count(FrameType type) => Sent.Count(x => x[0] == (byte)type);
        public int Signals(SignalType type) => Sent.Count(x => BoltCodec.TryReadCallSignal(x, out var header) && header.SignalType == type);
        /// <summary>Deliver a frame without waiting for it to be processed (it may close the connection).</summary>
        public void Send(byte[] frame) => inbound.Writer.TryWrite((frame, null));
        public async Task ProcessAsync(byte[] frame)
        {
            var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await inbound.Writer.WriteAsync((frame, null)); await inbound.Writer.WriteAsync((null, barrier));
            await barrier.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        private TaskCompletionSource? blocked;
        /// <summary>From now on every write waits, as on a link whose socket buffer is full.</summary>
        public void Block() => blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Release() => blocked?.TrySetResult();
        public List<(uint Sequence, bool Keyframe)> Media(Guid stream) => Sent
            .Where(x => x[0] == (byte)FrameType.MediaFrame && BoltCodec.TryReadMediaFrame(x, out var h) && h.StreamId == stream)
            .Select(x => { BoltCodec.TryReadMediaFrame(x, out var h); return (h.SequenceNumber, h.IsKeyframe); }).ToList();
        public int KeyRequests(Guid stream) => Sent.Count(x =>
            x[0] == (byte)FrameType.MediaKeyRequest && BoltCodec.TryReadMediaKeyRequest(x, out var id) && id == stream);
        public List<MediaCongestionData> Congestion(Guid stream) => Sent
            .Select(x => BoltCodec.TryReadMediaCongestion(x, out var report) ? report : (MediaCongestionData?)null)
            .Where(x => x is { } report && report.StreamId == stream).Select(x => x!.Value).ToList();
        public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            if (blocked is { } gate) await gate.Task.WaitAsync(ct);
            Sent.Enqueue(data.ToArray());
        }
        public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            while (await inbound.Reader.WaitToReadAsync(ct))
            {
                if (!inbound.Reader.TryRead(out var item)) continue;
                if (item.Barrier is { } barrier) { barrier.TrySetResult(); continue; }
                var frame = item.Frame!; frame.CopyTo(buffer); return (frame.Length, true);
            }
            return (0, true);
        }
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask CloseAsync(CancellationToken ct = default) => DisposeAsync();
        public ValueTask DisposeAsync() { IsConnected = false; inbound.Writer.TryComplete(); return ValueTask.CompletedTask; }
    }
}
