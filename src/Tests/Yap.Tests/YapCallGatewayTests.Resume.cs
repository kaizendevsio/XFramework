using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Claims;
using Bolt.Protocol;
using Bolt.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NUnit.Framework;
using Yap.Contracts;
using Yap.Services;

namespace Yap.Tests;

/// <summary>
/// Resumable calls, server side, over real WebSocket connections into the real relay: a dropped
/// connection holds the seat instead of ending the call, the phone comes back with a single-use,
/// seat-bound resume ticket, and only a refusal or the end of the grace period gives the seat up.
/// </summary>
public sealed partial class YapCallGatewayTests
{
    [Test]
    public async Task DroppedConnection_HoldsTheSeat_AndTheCallGoesOnForTheOtherParty()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        await using var call = await LiveCall.StartAsync(f);
        var events = new ConcurrentQueue<YapCallEvent>();
        using var _ = f.Gateway.Subscribe(f.Alice, events.Enqueue);

        await call.Bob.DropAsync();

        var roster = await UntilAsync(() => f.Gateway.GroupRoster(f.Alice, call.Id), x => Bob(x).Reconnecting);
        Assert.Multiple(() =>
        {
            Assert.That(Bob(roster).Left, Is.False, "Bob is still in the call");
            Assert.That(roster.Revision, Is.EqualTo(call.Revision), "nothing about the membership changed, so nobody rekeys");
            Assert.That(roster.Participants.Single(x => x.CredentialId != f.BobId).Reconnecting, Is.False);
            Assert.That(events.Select(x => x.Type), Does.Not.Contain("group-ended"), "the other side never sees the call end");
            Assert.That(call.Alice.Closed, Is.False, "and keeps its own connection");
        });
    }

    [Test]
    public async Task Resume_WithAFreshTicket_RejoinsTheSameEpoch()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        await using var call = await LiveCall.StartAsync(f);
        var join = call.BobTicket;
        await call.Bob.DropAsync();
        await UntilAsync(() => f.Gateway.GroupRoster(f.Alice, call.Id), x => Bob(x).Reconnecting);

        var resume = await f.Gateway.ResumeGroupAsync(f.Bob, call.Id, f.BobDevice);
        Assert.Multiple(() =>
        {
            Assert.That(resume.Url, Is.Not.EqualTo(join.Url), "a resume never reuses the spent join ticket");
            Assert.That(resume.ClientId, Is.EqualTo(YapCallGateway.ClientId(call.Id, f.BobId)), "the same media identity, so the same SFrame sender");
        });
        await using var back = await LiveSocket.ConnectAsync(f.Gateway, f.Bob, resume);
        await f.Gateway.ReadyGroupAsync(f.Bob, call.Id, revision: call.Revision);

        var roster = f.Gateway.GroupRoster(f.Alice, call.Id);
        Assert.Multiple(() =>
        {
            Assert.That(Bob(roster).Reconnecting, Is.False);
            Assert.That(Bob(roster).Ready, Is.True);
            Assert.That(roster.Revision, Is.EqualTo(call.Revision), "resumed within the epoch it left");
        });
    }

    [Test]
    public async Task GraceExpiry_EndsATwoPersonCall_WithAConnectionLostReason()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        await using var call = await LiveCall.StartAsync(f);
        var events = new ConcurrentQueue<YapCallEvent>();
        using var _ = f.Gateway.Subscribe(f.Alice, events.Enqueue);
        await call.Bob.DropAsync();
        await UntilAsync(() => f.Gateway.GroupRoster(f.Alice, call.Id), x => Bob(x).Reconnecting);

        f.Gateway.ExpireHolds();
        Assert.That(f.Gateway.GroupRoster(f.Alice, call.Id), Is.Not.Null, "a hold that has not run out is not ended early");

        Member(f, call.Id, f.BobId).GetType().GetField("HoldUntil")!.SetValue(Member(f, call.Id, f.BobId), (DateTimeOffset?)DateTimeOffset.UtcNow.AddSeconds(-1));
        f.Gateway.ExpireHolds();

        var ended = await UntilAsync(() => events.LastOrDefault(x => x.Type == "group-ended"), x => x is not null);
        Assert.Multiple(() =>
        {
            Assert.That(ended!.Reason, Is.EqualTo("connection-lost"), "the other side is told why");
            Assert.Throws<YapApiException>(() => f.Gateway.GroupRoster(f.Alice, call.Id));
            Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ResumeGroupAsync(f.Bob, call.Id, f.BobDevice))!.Status, Is.EqualTo(404),
                "a seat that was given up cannot be resumed");
        });
    }

    [Test]
    public async Task ResumeTicket_IsSingleUse()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        await using var call = await LiveCall.StartAsync(f);
        await call.Bob.DropAsync();
        await UntilAsync(() => f.Gateway.GroupRoster(f.Alice, call.Id), x => Bob(x).Reconnecting);
        var resume = await f.Gateway.ResumeGroupAsync(f.Bob, call.Id, f.BobDevice);
        await using var back = await LiveSocket.ConnectAsync(f.Gateway, f.Bob, resume);

        var replay = Assert.ThrowsAsync<YapApiException>(() => f.Gateway.AcceptSocketAsync(Context(f.Bob, resume.Url)));
        Assert.That(replay!.Status, Is.EqualTo(403), "a spent resume ticket admits nothing, not even its own owner");
        Assert.That(back.Closed, Is.False, "and the replay attempt does not disturb the connection it admitted");
    }

    [Test]
    public async Task ResumeTicket_IsBoundToTheAccount_Session_AndDevice()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        await using var call = await LiveCall.StartAsync(f);
        await call.Bob.DropAsync();
        await UntilAsync(() => f.Gateway.GroupRoster(f.Alice, call.Id), x => Bob(x).Reconnecting);

        Assert.Multiple(() =>
        {
            Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ResumeGroupAsync(f.Bob, call.Id, Guid.NewGuid()))!.Status,
                Is.EqualTo(403), "another device of the same account");
            Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ResumeGroupAsync(OtherSession(f.Bob), call.Id, f.BobDevice))!.Status,
                Is.EqualTo(403), "another sign-in of the same account");
            Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ResumeGroupAsync(f.OtherTenant, call.Id, f.BobDevice))!.Status,
                Is.EqualTo(404), "another account");
        });
        var resume = await f.Gateway.ResumeGroupAsync(f.Bob, call.Id, f.BobDevice);
        Assert.Multiple(() =>
        {
            Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.AcceptSocketAsync(Context(f.Alice, resume.Url)))!.Status,
                Is.EqualTo(403), "the other participant cannot use it");
            Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.AcceptSocketAsync(Context(OtherSession(f.Bob), resume.Url)))!.Status,
                Is.EqualTo(403), "nor can Bob's other sign-in");
        });
    }

    [Test]
    public async Task ResumeTicket_RotatesOnReissue_AndIsVoidedByLeaving()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        await using var call = await LiveCall.StartAsync(f, participants: 3);
        await call.Bob.DropAsync();
        await UntilAsync(() => f.Gateway.GroupRoster(f.Alice, call.Id), x => Bob(x).Reconnecting);

        var first = await f.Gateway.ResumeGroupAsync(f.Bob, call.Id, f.BobDevice);
        var second = await f.Gateway.ResumeGroupAsync(f.Bob, call.Id, f.BobDevice);
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.AcceptSocketAsync(Context(f.Bob, first.Url)))!.Status,
            Is.EqualTo(403), "issuing a ticket voids the one before it");

        await f.Gateway.LeaveGroupAsync(f.Bob, call.Id);
        Assert.Multiple(() =>
        {
            Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.AcceptSocketAsync(Context(f.Bob, second.Url)))!.Status,
                Is.EqualTo(403), "hanging up voids the outstanding ticket");
            Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ResumeGroupAsync(f.Bob, call.Id, f.BobDevice))!.Status,
                Is.EqualTo(404), "and no new one is issued");
            Assert.That(f.Gateway.GroupRoster(f.Alice, call.Id).Participants.Count(x => !x.Left), Is.EqualTo(2), "the others carry on");
        });
    }

    [Test]
    public async Task JoinTicket_IsNeverAResume()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        await using var call = await LiveCall.StartAsync(f);
        await call.Bob.DropAsync();
        await UntilAsync(() => f.Gateway.GroupRoster(f.Alice, call.Id), x => Bob(x).Reconnecting);
        Assert.Multiple(() =>
        {
            Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.AcceptSocketAsync(Context(f.Bob, call.BobTicket.Url)))!.Status,
                Is.EqualTo(403), "the spent join ticket stays spent");
            Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ConnectGroupAsync(f.Bob, call.Id))!.Status,
                Is.EqualTo(409), "and a seat that had a connection is not joined afresh");
        });
    }

    [TestCase("removed-from-thread")]
    [TestCase("device-revoked")]
    public async Task RevokedParticipant_CannotResume_AndLosesTheSeat(string revocation)
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        await using var call = await LiveCall.StartAsync(f, participants: 3);
        await call.Bob.DropAsync();
        await UntilAsync(() => f.Gateway.GroupRoster(f.Alice, call.Id), x => Bob(x).Reconnecting);
        if (revocation == "removed-from-thread") f.Members.RemoveAll(x => x.CredentialId == f.BobId);
        else f.RevokedDevices.Add(f.BobDevice);

        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ResumeGroupAsync(f.Bob, call.Id, f.BobDevice))!.Status, Is.EqualTo(403));
        Assert.Multiple(() =>
        {
            Assert.That(Bob(f.Gateway.GroupRoster(f.Alice, call.Id)).Left, Is.True, "a refusal gives the seat up at once");
            Assert.That(f.Gateway.GroupRoster(f.Alice, call.Id).Revision, Is.GreaterThan(call.Revision), "and rotates the keys without Bob");
        });
    }

    [Test]
    public async Task UnreachablePolicy_DoesNotEndAHeldSeat()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        await using var call = await LiveCall.StartAsync(f);
        await call.Bob.DropAsync();
        await UntilAsync(() => f.Gateway.GroupRoster(f.Alice, call.Id), x => Bob(x).Reconnecting);
        f.ThreadStatus = HttpStatusCode.ServiceUnavailable;

        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ResumeGroupAsync(f.Bob, call.Id, f.BobDevice))!.Status,
            Is.EqualTo(503), "not yet: the phone retries");
        Assert.That(Bob(f.Gateway.GroupRoster(f.Alice, call.Id)).Reconnecting, Is.True, "the seat is still held");
        f.ThreadStatus = HttpStatusCode.OK;
        Assert.That(await f.Gateway.ResumeGroupAsync(f.Bob, call.Id, f.BobDevice), Is.Not.Null);
    }

    [Test]
    public async Task EpochChangeWhileAway_KeepsTheSeat_AndTheResumeJoinsTheNewEpoch()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        await using var call = await LiveCall.StartAsync(f, participants: 3);
        await call.Bob.DropAsync();
        await UntilAsync(() => f.Gateway.GroupRoster(f.Alice, call.Id), x => Bob(x).Reconnecting);

        await f.Gateway.LeaveGroupAsync(f.Charlie, call.Id);
        var changed = f.Gateway.GroupRoster(f.Alice, call.Id);
        Assert.Multiple(() =>
        {
            Assert.That(changed.Revision, Is.GreaterThan(call.Revision), "Charlie leaving starts a new epoch");
            Assert.That(Bob(changed).Left, Is.False, "with Bob still in it");
            Assert.That(Bob(changed).Reconnecting, Is.True);
        });
        // Alice keys the new epoch for Bob while he is away; the key waits for him.
        await f.Gateway.RelayGroupControlAsync(f.Alice, call.Id, new(changed.Revision, 1, f.BobId, "opaque-new-epoch-key"));

        var resume = await f.Gateway.ResumeGroupAsync(f.Bob, call.Id, f.BobDevice);
        await using var back = await LiveSocket.ConnectAsync(f.Gateway, f.Bob, resume);
        var replayed = new List<YapCallEvent>();
        using (f.Gateway.Subscribe(f.Bob, replayed.Add)) { }
        Assert.That(replayed.Where(x => x.Type == "group-control").Select(x => x.Control!.Revision), Is.EqualTo(new[] { changed.Revision }),
            "Bob picks up the new epoch's key on his return");
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ReadyGroupAsync(f.Bob, call.Id, revision: call.Revision))!.Status,
            Is.EqualTo(409), "the old epoch is gone; resuming into it is refused");
        Assert.That(Bob(f.Gateway.GroupRoster(f.Alice, call.Id)).Left, Is.False, "and that refusal is not a hang-up");
        await f.Gateway.ReadyGroupAsync(f.Bob, call.Id, revision: changed.Revision);
        Assert.That(Bob(f.Gateway.GroupRoster(f.Alice, call.Id)).Reconnecting, Is.False);
    }

    /// <summary>An IP change: the phone is on a new network, the server still holds the old TCP connection.</summary>
    [Test]
    public async Task Resume_SupersedesAConnectionTheServerStillThinksIsAlive()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        await using var call = await LiveCall.StartAsync(f);
        var events = new ConcurrentQueue<YapCallEvent>();
        using var _ = f.Gateway.Subscribe(f.Alice, events.Enqueue);

        var resume = await f.Gateway.ResumeGroupAsync(f.Bob, call.Id, f.BobDevice);
        await using var back = await LiveSocket.ConnectAsync(f.Gateway, f.Bob, resume);
        Assert.That(() => call.Bob.Closed, Is.True.After(5000, 20), "the server lets go of the old connection");
        await f.Gateway.ReadyGroupAsync(f.Bob, call.Id, revision: call.Revision);
        await Task.Delay(200);

        var roster = f.Gateway.GroupRoster(f.Alice, call.Id);
        Assert.Multiple(() =>
        {
            Assert.That(Bob(roster).Left, Is.False);
            Assert.That(Bob(roster).Reconnecting, Is.False, "the old connection ending did not put the resumed seat on hold");
            Assert.That(roster.Revision, Is.EqualTo(call.Revision));
            Assert.That(back.Closed, Is.False);
            Assert.That(events.Select(x => x.Type), Does.Not.Contain("group-ended"));
        });
    }

    [Test]
    public async Task RelayRemovingASupersededConnection_IsNotTheParticipantLeaving()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        await using var call = await LiveCall.StartAsync(f);
        var member = Member(f, call.Id, f.BobId);
        var old = new ClaimsIdentity(f.Bob.Identity as ClaimsIdentity);
        old.AddClaim(new("yap_connection", "0"));
        var departed = typeof(YapCallGateway).GetMethod("GroupParticipantDeparted", BindingFlags.Instance | BindingFlags.NonPublic)!;

        departed.Invoke(f.Gateway, [new BoltGroupDeparture(call.Id, YapCallGateway.ClientId(call.Id, f.BobId), BoltGroupDepartureReason.Unauthorized, new ClaimsPrincipal(old))]);
        Assert.That(Bob(f.Gateway.GroupRoster(f.Alice, call.Id)).Left, Is.False, "a stale connection's refusal is not Bob's");

        var current = new ClaimsIdentity(f.Bob.Identity as ClaimsIdentity);
        current.AddClaim(new("yap_connection", member.GetType().GetField("Generation")!.GetValue(member)!.ToString()));
        departed.Invoke(f.Gateway, [new BoltGroupDeparture(call.Id, YapCallGateway.ClientId(call.Id, f.BobId), BoltGroupDepartureReason.Unauthorized, new ClaimsPrincipal(current))]);
        Assert.Throws<YapApiException>(() => f.Gateway.GroupRoster(f.Alice, call.Id), "the current connection's refusal ends Bob's seat, and with it this call");
    }

    [Test]
    public async Task RelayDroppingAStillOpenSocket_ClosesIt_SoThePhoneResumes()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        await using var call = await LiveCall.StartAsync(f);
        var member = Member(f, call.Id, f.BobId);
        var current = new ClaimsIdentity(f.Bob.Identity as ClaimsIdentity);
        current.AddClaim(new("yap_connection", member.GetType().GetField("Generation")!.GetValue(member)!.ToString()));
        typeof(YapCallGateway).GetMethod("GroupParticipantDeparted", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(f.Gateway, [new BoltGroupDeparture(call.Id, YapCallGateway.ClientId(call.Id, f.BobId), BoltGroupDepartureReason.Disconnected, new ClaimsPrincipal(current))]);

        Assert.That(() => call.Bob.Closed, Is.True.After(5000, 20), "a socket outside the relay's room is not left open");
        var roster = await UntilAsync(() => f.Gateway.GroupRoster(f.Alice, call.Id), x => Bob(x).Reconnecting);
        Assert.That(Bob(roster).Left, Is.False, "and its seat is held for the resume, not given up");
    }

    [Test]
    public async Task ReconnectGraceAndCallLength_AreConfigurable_WithSaneDefaults()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        Assert.Multiple(() =>
        {
            Assert.That(f.Gateway.ReconnectGrace, Is.EqualTo(TimeSpan.FromSeconds(45)));
            Assert.That(f.Gateway.MaxCallDuration, Is.EqualTo(TimeSpan.FromHours(12)), "the old one-hour cap is gone");
            Assert.That(f.Gateway.Server, Is.Not.Null);
        });
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId], deviceId: f.AliceDevice);
        var expires = (DateTimeOffset)RoomOf(f, room.Id).GetType().GetProperty("Expires")!.GetValue(RoomOf(f, room.Id))!;
        Assert.That(expires - DateTimeOffset.UtcNow, Is.GreaterThan(TimeSpan.FromHours(11.9)));
    }

    // ── Helpers ──

    /// <summary>Bob's row in a roster; the fixture's accounts are fresh per test, so the call remembers who Bob is.</summary>
    private static YapGroupParticipant Bob(YapGroupCall roster) => roster.Participants.Single(x => x.CredentialId == BobIds[roster.Id]);
    private static readonly ConcurrentDictionary<Guid, Guid> BobIds = new();

    private static ClaimsPrincipal OtherSession(ClaimsPrincipal user)
    {
        var identity = new ClaimsIdentity(user.Identity as ClaimsIdentity);
        identity.RemoveClaim(identity.FindFirst(YapAuth.SessionClaim)!);
        identity.AddClaim(new(YapAuth.SessionClaim, "another-session"));
        return new(identity);
    }

    private static object RoomOf(Fixture f, Guid call) =>
        ((System.Collections.IDictionary)typeof(YapCallGateway).GetField("groups", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Gateway)!)[call]!;

    private static object Member(Fixture f, Guid call, Guid credential)
    {
        var room = RoomOf(f, call);
        return ((System.Collections.IDictionary)room.GetType().GetProperty("Members")!.GetValue(room)!)[credential]!;
    }

    private static async Task<T> UntilAsync<T>(Func<T> read, Func<T, bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            var value = read();
            if (done(value)) return value;
            if (DateTime.UtcNow > deadline) Assert.Fail("The expected call state was not reached.");
            await Task.Delay(20);
        }
    }

    /// <summary>Alice calls Bob (and Charlie); everyone accepted is connected over a live socket and ready in the relay.</summary>
    private sealed class LiveCall : IAsyncDisposable
    {
        public Guid Id { get; private init; }
        public long Revision { get; private init; }
        public LiveSocket Alice { get; private init; } = null!;
        public LiveSocket Bob { get; private init; } = null!;
        public LiveSocket? Charlie { get; private init; }
        public YapCallConnection BobTicket { get; private init; } = null!;

        public static async Task<LiveCall> StartAsync(Fixture f, int participants = 2)
        {
            Guid[] invited = participants == 3 ? [f.BobId, f.CharlieId] : [f.BobId];
            var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, invited, deviceId: f.AliceDevice);
            BobIds[room.Id] = f.BobId;
            await f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice);
            if (participants == 3) await f.Gateway.AcceptGroupAsync(f.Charlie, room.Id, deviceId: f.CharlieDevice);
            var alice = await LiveSocket.ConnectAsync(f.Gateway, f.Alice, await f.Gateway.ConnectGroupAsync(f.Alice, room.Id));
            var bobTicket = await f.Gateway.ConnectGroupAsync(f.Bob, room.Id);
            var bob = await LiveSocket.ConnectAsync(f.Gateway, f.Bob, bobTicket);
            var charlie = participants == 3 ? await LiveSocket.ConnectAsync(f.Gateway, f.Charlie, await f.Gateway.ConnectGroupAsync(f.Charlie, room.Id)) : null;
            var revision = f.Gateway.GroupRoster(f.Alice, room.Id).Revision;
            await f.Gateway.ReadyGroupAsync(f.Alice, room.Id, revision: revision);
            await f.Gateway.ReadyGroupAsync(f.Bob, room.Id, revision: revision);
            if (participants == 3) await f.Gateway.ReadyGroupAsync(f.Charlie, room.Id, revision: revision);
            return new LiveCall { Id = room.Id, Revision = revision, Alice = alice, Bob = bob, Charlie = charlie, BobTicket = bobTicket };
        }

        public async ValueTask DisposeAsync()
        {
            await Alice.DisposeAsync();
            await Bob.DisposeAsync();
            if (Charlie is not null) await Charlie.DisposeAsync();
        }
    }

    /// <summary>
    /// A phone's call socket: a real WebSocket over loopback TCP into <see cref="YapCallGateway.AcceptSocketAsync"/>,
    /// registered with the relay under the server-issued identity.
    /// </summary>
    private sealed class LiveSocket : IAsyncDisposable
    {
        private readonly TcpClient tcp;
        private readonly WebSocket socket;
        private readonly Task receiving;
        private readonly CancellationTokenSource stop = new();
        public Task Serving { get; }
        public ConcurrentQueue<byte[]> Received { get; } = new();
        public bool Closed => receiving.IsCompleted;

        private LiveSocket(TcpClient tcp, WebSocket socket, Task serving)
        {
            this.tcp = tcp; this.socket = socket; Serving = serving;
            receiving = Task.Run(ReceiveAsync);
        }

        public static async Task<LiveSocket> ConnectAsync(YapCallGateway gateway, ClaimsPrincipal user, YapCallConnection connection)
        {
            var upgrade = new LiveUpgrade();
            var context = Context(user, connection.Url);
            context.Features.Set<IHttpWebSocketFeature>(upgrade);
            var serving = gateway.AcceptSocketAsync(context);
            var (tcp, client) = await upgrade.Client.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var live = new LiveSocket(tcp, client, serving);
            var register = new ArrayBufferWriter<byte>();
            BoltCodec.WriteRegister(register, connection.ClientId, "resume fixture");
            await client.SendAsync(register.WrittenMemory, WebSocketMessageType.Binary, true, CancellationToken.None);
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!live.Received.Any(x => x[0] == (byte)FrameType.RegisterAck && x[1] == 1))
            {
                if (DateTime.UtcNow > deadline || live.Closed) Assert.Fail("The call socket did not register.");
                await Task.Delay(10);
            }
            return live;
        }

        /// <summary>The network goes away under the phone: the TCP connection is reset, no close handshake.</summary>
        public async Task DropAsync()
        {
            tcp.Client.LingerState = new LingerOption(true, 0);
            tcp.Close();
            try { await Serving.WaitAsync(TimeSpan.FromSeconds(10)); } catch (Exception) when (Serving.IsCompleted) { }
        }

        private async Task ReceiveAsync()
        {
            var buffer = new byte[64 * 1024];
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(buffer, stop.Token);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    Received.Enqueue(buffer.AsSpan(0, result.Count).ToArray());
                }
            }
            catch (Exception) { /* Dropped, closed or disposed. */ }
        }

        public async ValueTask DisposeAsync()
        {
            stop.Cancel();
            socket.Abort();
            tcp.Dispose();
            try { await Serving.WaitAsync(TimeSpan.FromSeconds(10)); } catch { /* Already reported by the test, if it matters. */ }
        }
    }

    private sealed class LiveUpgrade : IHttpWebSocketFeature
    {
        public TaskCompletionSource<(TcpClient, WebSocket)> Client { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsWebSocketRequest => true;
        public async Task<WebSocket> AcceptAsync(WebSocketAcceptContext context)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var client = new TcpClient();
            var connecting = client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            var server = await listener.AcceptTcpClientAsync();
            await connecting;
            Client.TrySetResult((client, WebSocket.CreateFromStream(client.GetStream(), new WebSocketCreationOptions { IsServer = false })));
            return WebSocket.CreateFromStream(server.GetStream(), new WebSocketCreationOptions
            {
                IsServer = true,
                KeepAliveInterval = context.KeepAliveInterval ?? TimeSpan.Zero,
                KeepAliveTimeout = context.KeepAliveTimeout ?? Timeout.InfiniteTimeSpan
            });
        }
    }
}
