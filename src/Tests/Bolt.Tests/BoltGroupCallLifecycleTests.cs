using System.Buffers;
using System.Collections.Concurrent;
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
        await f.Send("a", stream);
        Assert.Multiple(() =>
        {
            Assert.That(f.Peers["b"].Count(FrameType.MediaFrame), Is.EqualTo(revoked == "a" ? 0 : 1));
            Assert.That(f.Peers["c"].Count(FrameType.MediaFrame), Is.Zero);
            Assert.That(calls[f.Call].Participants.Select(x => x.ClientId), Is.EquivalentTo(f.Peers.Keys.Where(x => x != revoked)));
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

    private static byte[] Frame(Action<IBufferWriter<byte>> write)
    { var writer = new ArrayBufferWriter<byte>(); write(writer); return writer.WrittenSpan.ToArray(); }

    private sealed class Policy(Guid call) : IBoltCallAuthorizer, IBoltGroupCallAuthorizer
    {
        public HashSet<string> Accepted { get; } = [];
        public Action? OnAuthorize;
        public ValueTask<bool> AuthorizeAsync(BoltCallAuthorizationContext context, CancellationToken ct = default) => ValueTask.FromResult(false);
        public ValueTask<bool> AuthorizeParticipantAsync(Guid id, string clientId, ClaimsPrincipal user, CancellationToken ct = default)
        {
            OnAuthorize?.Invoke();
            return ValueTask.FromResult(id == call && Accepted.Contains(clientId) && user.FindFirstValue("bolt_media_client_id") == clientId);
        }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Call { get; } = Guid.NewGuid();
        public Policy Policy { get; private set; } = null!;
        public BoltServer Server { get; private set; } = null!;
        public Dictionary<string, Peer> Peers { get; } = [];
        public Dictionary<string, Task> Tasks { get; } = [];
        public static async Task<Fixture> CreateAsync(bool policyEnabled = true, int limit = 8, int participants = 3)
        {
            var f = new Fixture(); f.Policy = new(f.Call);
            f.Server = new(NullLogger<BoltServer>.Instance, new BoltServerOptions
            { MediaEnabled = true, AuthenticatedMediaOnly = true, RequireSecureTransport = true, CallAuthorizer = f.Policy,
                GroupCallAuthorizer = policyEnabled ? f.Policy : null, MaxCallParticipants = limit });
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
        public Task Send(string id, Guid stream) => Peers[id].ProcessAsync(Frame(w => BoltCodec.WriteMediaFrame(w, stream, 1, 960, 0, [0xF8, 0xFF, 0xFE])));
        public async ValueTask DisposeAsync()
        { foreach (var peer in Peers.Values) await peer.DisposeAsync(); await Task.WhenAll(Tasks.Values).WaitAsync(TimeSpan.FromSeconds(5)); Server.Dispose(); }
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
        public async Task ProcessAsync(byte[] frame)
        {
            var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await inbound.Writer.WriteAsync((frame, null)); await inbound.Writer.WriteAsync((null, barrier));
            await barrier.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) { Sent.Enqueue(data.ToArray()); return ValueTask.CompletedTask; }
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
