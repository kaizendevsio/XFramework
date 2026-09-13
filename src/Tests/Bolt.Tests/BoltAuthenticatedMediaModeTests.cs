using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Channels;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Bolt.Server;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltAuthenticatedMediaModeTests
{
    [Test]
    public void DedicatedMode_WithoutSecureTransportRequirement_CannotStart()
    {
        var create = () => new BoltServer(NullLogger<BoltServer>.Instance,
            new BoltServerOptions { AuthenticatedMediaOnly = true, CallAuthorizer = new Policy() });
        create.Should().Throw<InvalidOperationException>();
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public async Task DedicatedMode_PlaintextOrUnauthenticated_ClosesBeforeRegistration(bool secure, bool authenticated)
    {
        using var server = Server(new Policy());
        await using var peer = new Peer();
        await server.HandleConnectionAsync(peer, Principal("alice", authenticated), CancellationToken.None, secure);
        peer.IsConnected.Should().BeFalse();
        peer.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task DedicatedMode_ForgedRegistration_RejectsIdentity()
    {
        using var server = Server(new Policy());
        await using var peer = new Peer();
        var running = server.HandleConnectionAsync(peer, Principal("alice"), CancellationToken.None, true);
        peer.Push(Frame(w => BoltCodec.WriteRegister(w, "bob", "Bob")));
        await running.WaitAsync(TimeSpan.FromSeconds(3));
        peer.IsConnected.Should().BeFalse();
        peer.Sent.Should().Contain(frame => frame[0] == (byte)FrameType.RegisterAck && frame[1] == 0);
    }

    [Test]
    public async Task DedicatedMode_RegisteredPeerSendsRpc_ClosesWithoutRouting()
    {
        using var server = Server(new Policy());
        await using var peer = new Peer();
        var running = server.HandleConnectionAsync(peer, Principal("alice"), CancellationToken.None, true);
        peer.Push(Frame(w => BoltCodec.WriteRegister(w, "alice", "Alice")));
        await WaitAsync(() => peer.Has(FrameType.RegisterAck));
        peer.Push([(byte)FrameType.Request]);
        await running.WaitAsync(TimeSpan.FromSeconds(3));
        peer.IsConnected.Should().BeFalse();
        peer.Sent.Should().HaveCount(1);
    }

    [Test]
    public async Task DedicatedMode_MembershipRevokedDuringCall_StopsMediaAtLeaseExpiry()
    {
        var policy = new Policy();
        using var server = Server(policy);
        await using var caller = new Peer();
        await using var callee = new Peer();
        var first = server.HandleConnectionAsync(caller, Principal("alice"), CancellationToken.None, true);
        var second = server.HandleConnectionAsync(callee, Principal("bob"), CancellationToken.None, true);
        try
        {
            caller.Push(Frame(w => BoltCodec.WriteRegister(w, "alice", "Alice")));
            callee.Push(Frame(w => BoltCodec.WriteRegister(w, "bob", "Bob")));
            await WaitAsync(() => caller.Has(FrameType.RegisterAck) && callee.Has(FrameType.RegisterAck));
            var callId = Guid.NewGuid();
            var payload = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(payload, BoltCodec.Fnv1aHash("bob"));
            caller.Push(Frame(w => BoltCodec.WriteCallSignal(w, callId, SignalType.Initiate, payload)));
            await WaitAsync(() => callee.HasSignal(SignalType.Initiate));
            callee.Push(Frame(w => BoltCodec.WriteCallSignal(w, callId, SignalType.Answer, [])));
            await WaitAsync(() => caller.HasSignal(SignalType.Answer));
            var streamId = Guid.NewGuid();
            caller.Push(Frame(w => BoltCodec.WriteMediaConfig(w, streamId, callId, MediaType.Audio, CodecId.Opus, 48000, 1, 32, 0, [])));
            await WaitAsync(() => callee.Has(FrameType.MediaConfig));
            var calls = (ConcurrentDictionary<Guid, ServerCallState>)typeof(BoltServer)
                .GetField("_activeCalls", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(server)!;
            policy.Allow = false;
            calls[callId].LastMediaAuthorizationTick = Environment.TickCount64 - 5001;
            caller.Push(Frame(w => BoltCodec.WriteMediaFrame(w, streamId, 1, 960, 0, [0xF8, 0xFF, 0xFE])));
            await WaitAsync(() => caller.HasSignal(SignalType.End) && callee.HasSignal(SignalType.End));
            callee.Has(FrameType.MediaFrame).Should().BeFalse();
            calls.Should().BeEmpty();
        }
        finally
        {
            await caller.DisposeAsync();
            await callee.DisposeAsync();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(3));
        }
    }

    private static BoltServer Server(Policy policy) => new(NullLogger<BoltServer>.Instance,
        new BoltServerOptions { MediaEnabled = true, AuthenticatedMediaOnly = true,
            RequireSecureTransport = true, CallAuthorizer = policy });
    private static ClaimsPrincipal Principal(string id, bool authenticated = true) => new(new ClaimsIdentity(
        [new Claim("sub", id), new Claim("bolt_media_client_id", id)], authenticated ? "fixture" : null));
    private static byte[] Frame(Action<IBufferWriter<byte>> write)
    { var writer = new ArrayBufferWriter<byte>(); write(writer); return writer.WrittenSpan.ToArray(); }
    private static async Task WaitAsync(Func<bool> ready)
    { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)); while (!ready()) await Task.Delay(5, timeout.Token); }
    private sealed class Policy : IBoltCallAuthorizer
    {
        public bool Allow = true;
        public ValueTask<bool> AuthorizeAsync(BoltCallAuthorizationContext context, CancellationToken ct = default) => ValueTask.FromResult(Allow);
    }
    private sealed class Peer : IBoltConnection
    {
        private readonly Channel<byte[]> inbound = Channel.CreateUnbounded<byte[]>();
        public ConcurrentQueue<byte[]> Sent { get; } = new();
        public bool IsConnected { get; private set; } = true;
        public bool SupportsDatagrams => false;
        public BoltTransport TransportType => BoltTransport.WebSocket;
        public void Push(byte[] frame) => inbound.Writer.TryWrite(frame);
        public bool Has(FrameType type) => Sent.Any(frame => frame[0] == (byte)type);
        public bool HasSignal(SignalType type) => Sent.Any(frame => BoltCodec.TryReadCallSignal(frame, out var signal) && signal.SignalType == type);
        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        { Sent.Enqueue(data.ToArray()); return ValueTask.CompletedTask; }
        public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (!await inbound.Reader.WaitToReadAsync(ct)) return (0, true);
            var frame = await inbound.Reader.ReadAsync(ct); frame.CopyTo(buffer); return (frame.Length, true);
        }
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask CloseAsync(CancellationToken ct = default) => DisposeAsync();
        public ValueTask DisposeAsync() { IsConnected = false; inbound.Writer.TryComplete(); return ValueTask.CompletedTask; }
    }
}
