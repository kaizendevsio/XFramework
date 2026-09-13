using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading.Channels;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Bolt.Server;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

// Real Bolt frame processing with authenticated principals; this is not browser/audio playback evidence.
[TestFixture]
public sealed class BoltMediaAuthorizationTests
{
    [Test]
    public async Task Initiate_WithoutHostAuthorization_DoesNotRingRecipient()
    {
        await using var fixture = new CallFixture(null);
        await fixture.ConnectAsync();
        await fixture.InitiateAsync(Guid.NewGuid());
        fixture.B.SignalCount(SignalType.Initiate).Should().Be(0);
    }

    [Test]
    public async Task Initiate_UnauthenticatedCaller_DoesNotInvokePermissivePolicy()
    {
        var policy = new CallPolicy();
        await using var fixture = new CallFixture(policy);
        await fixture.ConnectAsync(authenticateCaller: false);
        await fixture.InitiateAsync(Guid.NewGuid());
        policy.Calls.Should().Be(0);
        fixture.B.SignalCount(SignalType.Initiate).Should().Be(0);
    }

    [Test]
    public async Task Initiate_CrossTenantPolicyDenied_DoesNotRingRecipient()
    {
        var policy = new CallPolicy();
        await using var fixture = new CallFixture(policy);
        await fixture.ConnectAsync(recipientTenant: "other");
        await fixture.InitiateAsync(Guid.NewGuid());
        policy.Calls.Should().Be(1);
        fixture.B.SignalCount(SignalType.Initiate).Should().Be(0);
    }

    [Test]
    public async Task Initiate_PolicyThrows_FailsClosed()
    {
        await using var fixture = new CallFixture(new CallPolicy { Throw = true });
        await fixture.ConnectAsync();
        await fixture.InitiateAsync(Guid.NewGuid());
        fixture.B.SignalCount(SignalType.Initiate).Should().Be(0);
    }

    [Test]
    public async Task Answer_MembershipRevokedAfterRinging_DoesNotActivateCall()
    {
        var policy = new CallPolicy();
        await using var fixture = new CallFixture(policy);
        await fixture.ConnectAsync();
        var id = Guid.NewGuid();
        await fixture.InitiateAsync(id);
        await fixture.B.WaitForSignalAsync(SignalType.Initiate);
        policy.Allow = false;
        await fixture.B.ProcessAsync(Signal(id, SignalType.Answer));
        fixture.A.SignalCount(SignalType.Answer).Should().Be(0);
        policy.Calls.Should().Be(2);
    }

    [Test]
    public async Task Initiate_PrincipalQuotaReached_EndReleasesCapacity()
    {
        await using var fixture = new CallFixture(new CallPolicy(), callLimit: 1);
        await fixture.ConnectAsync();
        var first = Guid.NewGuid();
        await fixture.InitiateAsync(first);
        await fixture.B.WaitForSignalAsync(SignalType.Initiate);
        await fixture.InitiateAsync(Guid.NewGuid());
        fixture.B.SignalCount(SignalType.Initiate).Should().Be(1);
        await fixture.A.ProcessAsync(Signal(first, SignalType.End));
        await fixture.InitiateAsync(Guid.NewGuid());
        await fixture.B.WaitForSignalAsync(SignalType.Initiate, count: 2);
    }

    [Test]
    public async Task Initiate_GlobalQuotaReached_EndReleasesCapacity()
    {
        await using var fixture = new CallFixture(new CallPolicy(), callLimit: 10, globalLimit: 1);
        await fixture.ConnectAsync();
        var first = Guid.NewGuid();
        await fixture.InitiateAsync(first);
        await fixture.B.WaitForSignalAsync(SignalType.Initiate);
        await fixture.InitiateAsync(Guid.NewGuid());
        fixture.B.SignalCount(SignalType.Initiate).Should().Be(1);
        await fixture.A.ProcessAsync(Signal(first, SignalType.End));
        await fixture.InitiateAsync(Guid.NewGuid());
        await fixture.B.WaitForSignalAsync(SignalType.Initiate, count: 2);
    }

    [Test]
    public async Task AddParticipant_AtParticipantLimit_DoesNotInviteAnotherPeer()
    {
        await using var fixture = new CallFixture(new CallPolicy(), participantLimit: 2);
        await fixture.ConnectAsync();
        var call = Guid.NewGuid();
        await fixture.InitiateAsync(call);
        await fixture.B.WaitForSignalAsync(SignalType.Initiate);
        await fixture.B.ProcessAsync(Signal(call, SignalType.Answer));
        await fixture.A.WaitForSignalAsync(SignalType.Answer);
        var payload = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(payload, BoltCodec.Fnv1aHash("charlie"));
        await fixture.A.ProcessAsync(Frame(writer => BoltCodec.WriteCallSignal(writer, call, SignalType.AddParticipant, payload)));
        fixture.C.Sent.Count(bytes => bytes[0] == (byte)FrameType.CallSignal).Should().Be(0);
    }

    [Test]
    public async Task MediaFrame_AuthorizedAnsweredCall_ForwardsPayloadAndStopsAfterEnd()
    {
        await using var fixture = new CallFixture(new CallPolicy());
        await fixture.ConnectAsync();
        var call = Guid.NewGuid();
        await fixture.InitiateAsync(call);
        await fixture.B.WaitForSignalAsync(SignalType.Initiate);
        await fixture.B.ProcessAsync(Signal(call, SignalType.Answer));
        await fixture.A.WaitForSignalAsync(SignalType.Answer);
        var stream = Guid.NewGuid();
        await fixture.A.ProcessAsync(Frame(writer => BoltCodec.WriteMediaConfig(
            writer, stream, call, MediaType.Audio, CodecId.Opus, 48000, 1, 32, 0, [])));
        await fixture.B.WaitForFrameAsync(FrameType.MediaConfig);
        byte[] payload = [0xF8, 0xFF, 0xFE]; // Opus silence packet; transport assertions only.
        var media = Frame(writer => BoltCodec.WriteMediaFrame(writer, stream, 1, 960, 0, payload));
        await fixture.A.ProcessAsync(media);
        await fixture.B.WaitForFrameAsync(FrameType.MediaFrame);
        fixture.B.Sent.Single(bytes => bytes[0] == (byte)FrameType.MediaFrame).Should().Equal(media);
        await fixture.A.ProcessAsync(Signal(call, SignalType.End));
        await fixture.B.WaitForSignalAsync(SignalType.End);
        await fixture.A.ProcessAsync(media);
        fixture.B.Sent.Count(bytes => bytes[0] == (byte)FrameType.MediaFrame).Should().Be(1);
    }

    private static byte[] Frame(Action<IBufferWriter<byte>> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        write(buffer);
        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] Signal(Guid id, SignalType signal) =>
        Frame(writer => BoltCodec.WriteCallSignal(writer, id, signal, []));

    private sealed class CallPolicy : IBoltCallAuthorizer
    {
        public bool Allow { get; set; } = true;
        public bool Throw { get; init; }
        public int Calls { get; private set; }
        public ValueTask<bool> AuthorizeAsync(BoltCallAuthorizationContext context, CancellationToken ct = default)
        {
            Calls++;
            if (Throw) throw new InvalidOperationException("Fixture policy unavailable");
            return ValueTask.FromResult(Allow && context.Caller.FindFirst("tenant")?.Value ==
                context.Recipient.FindFirst("tenant")?.Value);
        }
    }

    private sealed class CallFixture : IAsyncDisposable
    {
        private readonly BoltServer server;
        private Task aTask = Task.CompletedTask;
        private Task bTask = Task.CompletedTask;
        private Task cTask = Task.CompletedTask;
        public Peer A { get; } = new();
        public Peer B { get; } = new();
        public Peer C { get; } = new();
        public CallFixture(IBoltCallAuthorizer? policy, int callLimit = 2, int globalLimit = 128, int participantLimit = 8) => server = new(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions { MediaEnabled = true, CallAuthorizer = policy, MaxActiveCallsPerPrincipal = callLimit,
                MaxActiveCalls = globalLimit, MaxCallParticipants = participantLimit });

        public async Task ConnectAsync(bool authenticateCaller = true, string recipientTenant = "tenant")
        {
            aTask = server.HandleConnectionAsync(A, Principal("alice", "tenant", authenticateCaller), CancellationToken.None);
            bTask = server.HandleConnectionAsync(B, Principal("bob", recipientTenant, true), CancellationToken.None);
            cTask = server.HandleConnectionAsync(C, Principal("charlie", "tenant", true), CancellationToken.None);
            await A.ProcessAsync(Frame(writer => BoltCodec.WriteRegister(writer, "alice", "Alice")));
            await B.ProcessAsync(Frame(writer => BoltCodec.WriteRegister(writer, "bob", "Bob")));
            await C.ProcessAsync(Frame(writer => BoltCodec.WriteRegister(writer, "charlie", "Charlie")));
            await Task.WhenAll(A.WaitForFrameAsync(FrameType.RegisterAck), B.WaitForFrameAsync(FrameType.RegisterAck), C.WaitForFrameAsync(FrameType.RegisterAck));
        }

        public Task InitiateAsync(Guid call)
        {
            var payload = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(payload, BoltCodec.Fnv1aHash("bob"));
            return A.ProcessAsync(Frame(writer => BoltCodec.WriteCallSignal(writer, call, SignalType.Initiate, payload)));
        }

        private static ClaimsPrincipal Principal(string id, string tenant, bool authenticated) => new(
            new ClaimsIdentity([new Claim("sub", id), new Claim("tenant", tenant)], authenticated ? "fixture" : null));

        public async ValueTask DisposeAsync()
        {
            await A.DisposeAsync();
            await B.DisposeAsync();
            await C.DisposeAsync();
            await Task.WhenAll(aTask, bTask, cTask).WaitAsync(TimeSpan.FromSeconds(5));
            server.Dispose();
        }
    }

    private sealed class Peer : IBoltConnection
    {
        private readonly Channel<(byte[]? Frame, TaskCompletionSource? Barrier)> incoming = Channel.CreateUnbounded<(byte[]?, TaskCompletionSource?)>();
        public ConcurrentQueue<byte[]> Sent { get; } = new();
        public bool SupportsDatagrams => false;
        public bool IsConnected { get; private set; } = true;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public async Task ProcessAsync(byte[] frame)
        {
            var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await incoming.Writer.WriteAsync((frame, null));
            await incoming.Writer.WriteAsync((null, processed));
            await processed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public int SignalCount(SignalType signal) => Sent.Count(bytes =>
            BoltCodec.TryReadCallSignal(bytes, out var header) && header.SignalType == signal);
        public Task WaitForSignalAsync(SignalType signal, int count = 1) => WaitForAsync(() => SignalCount(signal) >= count);
        public Task WaitForFrameAsync(FrameType type) => WaitForAsync(() => Sent.Any(bytes => bytes[0] == (byte)type));
        private static async Task WaitForAsync(Func<bool> ready)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!ready()) await Task.Delay(5, timeout.Token);
        }

        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            Sent.Enqueue(data.ToArray());
            return ValueTask.CompletedTask;
        }
        public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            while (await incoming.Reader.WaitToReadAsync(ct))
            {
                if (!incoming.Reader.TryRead(out var item)) continue;
                if (item.Barrier is not null) { item.Barrier.TrySetResult(); continue; }
                var frame = item.Frame!;
                frame.CopyTo(buffer);
                return (frame.Length, true);
            }
            IsConnected = false;
            return (0, true);
        }
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask CloseAsync(CancellationToken ct = default) => DisposeAsync();
        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            incoming.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
