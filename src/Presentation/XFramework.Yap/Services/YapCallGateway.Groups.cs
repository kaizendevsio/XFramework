using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Security.Cryptography;
using Bolt.Protocol.Transport;
using Bolt.Server;
using Yap.Contracts;

[assembly: InternalsVisibleTo("Yap.Tests")]

namespace Yap.Services;

public sealed partial class YapCallGateway
{
    private readonly bool groupLifecycleEnabled;
    private readonly bool videoEnabled;
    private readonly Dictionary<Guid, GroupRoom> groups = [];

    /// <summary>Cameras one call may carry at once. Eight voices is fine; eight live pictures is not.</summary>
    internal const int MaxVideoSenders = 4;

    // Admission requires the explicit encrypted-group configuration and approved devices.
    internal async Task<YapGroupCall> StartGroupAsync(ClaimsPrincipal user, Guid thread, IReadOnlyCollection<Guid> recipients, CancellationToken ct = default, Guid deviceId = default, bool videoRequested = false)
    {
        RequireGroupLifecycle();
        var (tenant, caller) = Identity(user);
        if (thread == Guid.Empty || recipients.Count is < 1 or > 7 || recipients.Contains(Guid.Empty) ||
            recipients.Contains(caller) || recipients.Distinct().Count() != recipients.Count)
            throw new YapApiException(400, "Choose between one and seven other conversation members.");
        var members = await CurrentMembersAsync(user, thread, ct);
        if (recipients.Any(x => !members.Contains(x))) throw new YapApiException(403, "Only current conversation members can join.");
        await VerifyCallDeviceAsync(user, deviceId, ct);
        var room = new GroupRoom(Guid.NewGuid(), tenant, thread, caller, user.Identity?.Name ?? "Someone", MaxCallDuration) { VideoRequested = videoRequested && VideoEnabled };
        room.Members.Add(caller, new GroupMember { Session = user.FindFirstValue(YapAuth.SessionClaim), Accepted = true, DeviceId = deviceId });
        foreach (var id in recipients) room.Members.Add(id, new GroupMember());
        YapGroupCall snapshot;
        lock (gate)
        {
            if (groups.Count + invites.Count >= 64 || room.Members.Keys.Any(id => GroupMemberBusy(tenant, id) ||
                invites.Values.Any(x => x.Tenant == tenant && (x.Invite.CallerId == id || x.Invite.RecipientId == id))))
                throw new YapApiException(409, "Someone is already in a call.");
            groups.Add(room.Id, room);
            PublishGroupLocked(room, "group-incoming");
            snapshot = Snapshot(room);
        }

        // Outside the lock: a push round trip must never hold the gateway's single mutex.
        NotifyIncomingCall(tenant, thread, room.Id, room.InviteExpires, recipients);
        return snapshot;
    }

    internal async Task<YapGroupCall> AcceptGroupAsync(ClaimsPrincipal user, Guid callId, CancellationToken ct = default, Guid deviceId = default)
    {
        RequireGroupLifecycle();
        var (tenant, credential) = Identity(user);
        GroupRoom room;
        lock (gate) room = GetGroup(tenant, credential, callId);
        await VerifyMembershipAsync(user, room.Thread, credential, ct);
        await VerifyCallDeviceAsync(user, deviceId, ct);
        lock (gate)
        {
            room = GetGroup(tenant, credential, callId);
            var member = room.Members[credential];
            var session = user.FindFirstValue(YapAuth.SessionClaim);
            if (member.Accepted && (member.Session != session || member.DeviceId != deviceId)) throw new YapApiException(409, "This call is already accepted on another device.");
            if (!member.Accepted && room.InviteExpires <= DateTimeOffset.UtcNow) throw new YapApiException(410, "This invitation expired.");
            if (!member.Accepted)
            {
                member.Accepted = true; member.Session = session; member.DeviceId = deviceId;
                AdvanceGroupRoster(room);
                PublishGroupLocked(room, "group-roster");
            }
            return Snapshot(room);
        }
    }

    internal async Task<Yap.Contracts.YapCallConnection> ConnectGroupAsync(ClaimsPrincipal user, Guid callId, CancellationToken ct = default)
    {
        RequireGroupLifecycle();
        var (tenant, credential) = Identity(user);
        GroupRoom room;
        lock (gate) { room = GetGroup(tenant, credential, callId); RequireAccepted(room.Members[credential], user); }
        await VerifyMembershipAsync(user, room.Thread, credential, ct);
        await VerifyCallDeviceAsync(user, room.Members[credential].DeviceId, ct);
        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        lock (gate)
        {
            room = GetGroup(tenant, credential, callId);
            var member = room.Members[credential];
            RequireAccepted(member, user);
            if (member.Connected) throw new YapApiException(409, "This call is already connected.");
            // A seat that has had a connection comes back through the resume ticket, which is bound to it.
            if (member.Generation > 0) throw new YapApiException(409, "Resume this call instead.");
            RemoveGroupTickets(callId, credential);
            tickets[TicketKey(ticket)] = new(callId, tenant, credential, member.Session!, DateTimeOffset.UtcNow.AddSeconds(30), Group: true);
        }
        // A host-managed group has no single recipient. The group client obtains its accepted roster separately.
        return new(callId, $"/api/chat/calls/socket?ticket={ticket}", ClientId(callId, credential), "");
    }

    internal async Task ReadyGroupAsync(ClaimsPrincipal user, Guid callId, CancellationToken ct = default, long? revision = null)
    {
        var (tenant, credential) = Identity(user);
        GroupRoom room;
        bool resuming;
        lock (gate)
        {
            room = GetGroup(tenant, credential, callId);
            if (revision is { } expected && room.Revision != expected) throw new YapApiException(409, "The call membership changed.");
            var member = room.Members[credential];
            RequireAccepted(member, user);
            if (!member.Registered || !member.Connected) throw new YapApiException(409, "Connect before joining this call.");
            resuming = member.AwaySince is not null;
        }
        if (!await Server.JoinGroupCallAsync(callId, ClientId(callId, credential), ct))
        {
            // Admission stays fail-closed: no media without a positive answer. A resuming participant
            // keeps the seat it is already holding, though, until the hold runs out: an answer that could
            // not be given right now must not be what ends the call.
            if (resuming) throw new YapApiException(503, "The call could not be rejoined yet. Try again.");
            await RemoveGroupMemberAsync(room, credential, ct);
            throw new YapApiException(403, "This call is no longer available.");
        }
        lock (gate)
        {
            room = GetGroup(tenant, credential, callId);
            if (revision is { } expected && room.Revision != expected) throw new YapApiException(409, "The call membership changed.");
            var member = room.Members[credential];
            member.Ready = true;
            // Back in the relay's room: the seat is no longer held, it is occupied (unless the new
            // socket already dropped again, in which case its hold stands).
            if (member.Connected) { member.AwaySince = null; member.HoldUntil = null; }
            if (room.Members.Values.Count(x => x.Ready && !x.Left) >= 2)
            { room.Started = true; room.ConnectedAt ??= DateTimeOffset.UtcNow; }
            PublishGroupLocked(room, "group-roster");
        }
    }

    internal async Task LeaveGroupAsync(ClaimsPrincipal user, Guid callId, CancellationToken ct = default)
    {
        var (tenant, credential) = Identity(user);
        GroupRoom room;
        lock (gate) room = GetGroup(tenant, credential, callId);
        await RemoveGroupMemberAsync(room, credential, ct);
    }

    internal YapGroupCall GroupRoster(ClaimsPrincipal user, Guid callId)
    {
        var (tenant, credential) = Identity(user);
        lock (gate) return Snapshot(GetGroup(tenant, credential, callId));
    }

    public async ValueTask<bool> AuthorizeParticipantAsync(Guid callId, string clientId, ClaimsPrincipal participant, CancellationToken ct = default)
    {
        if (!groupLifecycleEnabled || participant.Identity?.IsAuthenticated != true) return false;
        var (tenant, credential) = Identity(participant);
        GroupRoom room;
        lock (gate) if (!IsSeated(callId, clientId, participant, tenant, credential, out room)) return false;
        await VerifyMembershipAsync(participant, room.Thread, credential, ct);
        await VerifyCallDeviceAsync(participant, room.Members[credential].DeviceId, ct);
        lock (gate) return IsStillSeated(room, callId, participant, credential);
    }

    /// <summary>
    /// The relay's periodic re-check. Local seat facts and definitive downstream refusals remove the
    /// participant; an unreachable or failing directory only reports that the answer is unavailable,
    /// so a hub reconnect or a slow backend cannot end a call that is otherwise still allowed.
    /// </summary>
    public async ValueTask<BoltGroupAuthorizationDecision> RenewParticipantAsync(Guid callId, string clientId, ClaimsPrincipal participant, CancellationToken ct = default)
    {
        if (!groupLifecycleEnabled || participant.Identity?.IsAuthenticated != true) return BoltGroupAuthorizationDecision.Denied;
        var (tenant, credential) = Identity(participant);
        GroupRoom room;
        Guid deviceId;
        lock (gate)
        {
            if (!IsSeated(callId, clientId, participant, tenant, credential, out room)) return BoltGroupAuthorizationDecision.Denied;
            deviceId = room.Members[credential].DeviceId;
        }
        try
        {
            var membership = await CheckMembershipAsync(participant, room.Thread, credential, ct);
            if (membership != BoltGroupAuthorizationDecision.Allowed) return membership;
            var device = await CheckCallDeviceAsync(participant, deviceId, ct);
            if (device != BoltGroupAuthorizationDecision.Allowed) return device;
        }
        catch (Exception error) { return Classify(error); }
        lock (gate) return IsStillSeated(room, callId, participant, credential) ? BoltGroupAuthorizationDecision.Allowed : BoltGroupAuthorizationDecision.Denied;
    }

    private bool IsSeated(Guid callId, string clientId, ClaimsPrincipal participant, Guid tenant, Guid credential, out GroupRoom room) =>
        groups.TryGetValue(callId, out room!) && room.Tenant == tenant && room.Expires > DateTimeOffset.UtcNow &&
        room.Members.TryGetValue(credential, out var member) && !member.Left && member.Accepted && member.Registered &&
        member.Connected && member.Session == participant.FindFirstValue(YapAuth.SessionClaim) &&
        clientId == ClientId(callId, credential) && participant.FindFirstValue("bolt_media_client_id") == clientId &&
        participant.FindFirstValue("yap_call_id") == callId.ToString() && IsCurrentConnection(participant, member);

    /// <summary>
    /// Each socket's principal names the seat generation it was admitted as. After a resume the old
    /// socket is no longer seated, whatever its client ID says, so the relay removes it and the host
    /// knows that removal is not the participant leaving.
    /// </summary>
    private const string ConnectionClaim = "yap_connection";
    private static bool IsCurrentConnection(ClaimsPrincipal participant, GroupMember member) =>
        participant.FindFirstValue(ConnectionClaim) == member.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private bool IsStillSeated(GroupRoom room, Guid callId, ClaimsPrincipal participant, Guid credential) =>
        groups.TryGetValue(callId, out var current) && ReferenceEquals(room, current) && room.Expires > DateTimeOffset.UtcNow &&
        room.Members.TryGetValue(credential, out var member) && !member.Left && member.Connected && member.Registered && member.Accepted &&
        member.Session == participant.FindFirstValue(YapAuth.SessionClaim) && IsCurrentConnection(participant, member);

    /// <summary>
    /// Upgrade one call socket. <paramref name="token"/> is the ticket's hash. A join ticket admits the
    /// seat's first connection; a resume ticket admits its replacement, superseding a previous socket
    /// that the server still believes is alive (an IP change leaves exactly that behind).
    /// </summary>
    private async Task AcceptGroupSocketAsync(HttpContext context, string token)
    {
        RequireGroupLifecycle();
        var (tenant, credential) = Identity(context.User);
        GroupRoom room;
        GroupMember member;
        int generation;
        CancellationTokenSource connection;
        TaskCompletionSource closed;
        CancellationTokenSource? superseded = null;
        Task? previous = null;
        lock (gate)
        {
            if (!tickets.TryGetValue(token, out var ticket) || !ticket.Group || ticket.ExpiresAt <= DateTimeOffset.UtcNow ||
                ticket.Tenant != tenant || ticket.User != credential || ticket.Session != context.User.FindFirstValue(YapAuth.SessionClaim))
                throw new YapApiException(403, "This call connection has expired.");
            room = GetGroup(tenant, credential, ticket.CallId);
            member = room.Members[credential];
            RequireAccepted(member, context.User);
            // Consumed before anything can fail or wait: a ticket is never good for a second upgrade.
            tickets.Remove(token);
            if (ticket.Resume)
            {
                // Bound to the seat as it was when the ticket was issued, on this device. A newer resume
                // (or the join of a different seat) makes it worthless.
                if (ticket.Generation != member.Generation || ticket.DeviceId != member.DeviceId)
                    throw new YapApiException(403, "This call connection has expired.");
                if (member.Connected)
                {
                    superseded = member.Connection;
                    previous = member.Closed?.Task;
                }
                if (member.AwaySince is null) { member.AwaySince = DateTimeOffset.UtcNow; PublishGroupLocked(room, "group-roster"); }
                ExtendHoldForResumeLocked(member);
            }
            else if (member.Connected || member.Generation > 0) throw new YapApiException(409, "This call is already connected.");
            generation = ++member.Generation;
            member.Connected = true;
            member.Registered = false;
            connection = member.Connection = CancellationTokenSource.CreateLinkedTokenSource(member.Lifetime.Token);
            closed = member.Closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        try
        {
            // The relay registers one connection per participant: let the old one finish leaving first.
            try { superseded?.Cancel(); } catch (ObjectDisposedException) { /* It ended on its own meanwhile. */ }
            if (previous is not null)
            {
                try { await previous.WaitAsync(TimeSpan.FromSeconds(10), context.RequestAborted); }
                catch (TimeoutException) { /* The new socket's registration will say whether it is gone. */ }
            }
            await VerifyMembershipAsync(context.User, room.Thread, credential, context.RequestAborted);
            lock (gate)
            {
                GetGroup(tenant, credential, room.Id); RequireAccepted(member, context.User);
                if (member.Generation != generation) throw new YapApiException(409, "This call is already connected.");
            }
            var identity = new ClaimsIdentity(context.User.Identity as ClaimsIdentity);
            identity.AddClaim(new("bolt_media_client_id", ClientId(room.Id, credential)));
            identity.AddClaim(new("yap_call_id", room.Id.ToString()));
            identity.AddClaim(new(ConnectionClaim, generation.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, connection.Token);
            var remaining = room.Expires - DateTimeOffset.UtcNow;
            lifetime.CancelAfter(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
            LimitUnsentBytes(context);
            using var socket = await context.WebSockets.AcceptWebSocketAsync(SocketOptions());
            await using var transport = new ReadyTransport(new WebSocketBoltConnection(socket), () =>
            {
                lock (gate)
                {
                    if (member.Left || member.Generation != generation) return;
                    member.Registered = member.EverRegistered = true;
                }
            });
            await Server.HandleConnectionAsync(transport, new ClaimsPrincipal(identity), lifetime.Token, isSecureTransport: true);
        }
        finally
        {
            closed.TrySetResult();
            lock (gate) if (ReferenceEquals(member.Connection, connection)) member.Connection = null;
            connection.Dispose();
            await ConnectionEndedAsync(room, credential, generation);
        }
    }

    /// <summary>
    /// A call socket ended. A participant who had a working connection keeps the seat for
    /// <see cref="ReconnectGrace"/> and is shown as reconnecting; the call goes on for everyone else
    /// and the key epoch is untouched. A connection that never got going, a superseded one, and a
    /// participant who already left change nothing here.
    /// </summary>
    private async Task ConnectionEndedAsync(GroupRoom room, Guid credential, int generation)
    {
        lock (gate)
        {
            if (!room.Members.TryGetValue(credential, out var member) || member.Left) return;
            // Superseded by a resume: the newer connection owns the seat now.
            if (member.Generation != generation) return;
            member.Connected = false;
            member.Registered = false;
            if (member.EverRegistered && groups.TryGetValue(room.Id, out var current) && ReferenceEquals(current, room))
            {
                var now = DateTimeOffset.UtcNow;
                member.AwaySince ??= now;
                // A seat that keeps dropping before it rejoins is not held forever: the grace counts from
                // the first drop, plus at most one resume window.
                var until = now + ReconnectGrace;
                var cap = member.AwaySince.Value + ReconnectGrace + ResumeWindow;
                member.HoldUntil = until < cap ? until : cap;
                PublishGroupLocked(room, "group-roster");
                return;
            }
        }
        await RemoveGroupMemberAsync(room, credential, CancellationToken.None);
    }

    /// <summary>
    /// A resumed connection gets a bounded window to rejoin (and rekey) once its socket is up: at least
    /// <see cref="ResumeWindow"/> from now, never beyond one grace period plus one window after the drop.
    /// </summary>
    private void ExtendHoldForResumeLocked(GroupMember member)
    {
        var now = DateTimeOffset.UtcNow;
        var wanted = now + ResumeWindow;
        if (member.HoldUntil is { } held && held > wanted) wanted = held;
        var cap = (member.AwaySince ?? now) + ReconnectGrace + ResumeWindow;
        member.HoldUntil = wanted < cap ? wanted : cap;
    }

    /// <summary>End seats whose hold ran out: the participant did not come back in time.</summary>
    internal void ExpireHolds()
    {
        (GroupRoom Room, Guid Credential)[] expired;
        var now = DateTimeOffset.UtcNow;
        lock (gate)
            expired = groups.Values.SelectMany(room => room.Members
                    .Where(x => !x.Value.Left && x.Value.HoldUntil is { } until && until <= now)
                    .Select(x => (room, x.Key)))
                .ToArray();
        foreach (var (room, credential) in expired)
            _ = RemoveGroupMemberAsync(room, credential, CancellationToken.None, "connection-lost",
                member => member.HoldUntil is { } until && until <= DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Leave, hang up, a refusal, or a seat hold that ran out. <paramref name="reason"/> is published with
    /// the <c>group-ended</c> it may cause; <paramref name="onlyIf"/> is re-checked under the lock so a
    /// participant who resumed at the last moment is not removed by a decision made just before.
    /// </summary>
    private async Task RemoveGroupMemberAsync(GroupRoom room, Guid credential, CancellationToken ct, string? reason = null,
        Func<GroupMember, bool>? onlyIf = null)
    {
        GroupMember[] cancel;
        lock (gate)
        {
            if (!room.Members.TryGetValue(credential, out var member) || member.Left || onlyIf?.Invoke(member) == false) return;
            member.Left = true; member.Ready = false; member.Registered = false; member.Connected = false; member.Video = false;
            member.AwaySince = null; member.HoldUntil = null;
            if (member.Accepted) AdvanceGroupRoster(room);
            Publish(room.Tenant, credential, GroupEvent(room, credential, "group-roster", reason: reason));
            PublishGroupLocked(room, "group-roster", reason);
            RemoveGroupTickets(room.Id, credential);
            cancel = [member];
            if (!CanContinueGroup(room))
            {
                RemoveGroupLocked(room);
                cancel = room.Members.Values.ToArray();
                foreach (var id in room.Members.Keys) { room.Members[id].Left = true; RemoveGroupTickets(room.Id, id); }
                PublishGroupLocked(room, "group-ended", reason);
            }
        }
        foreach (var member in cancel) member.Lifetime.Cancel();
        await Server.LeaveGroupCallAsync(room.Id, ClientId(room.Id, credential), ct);
    }

    /// <summary>
    /// The relay dropped a participant. A lost transport is the socket's business (it holds the seat);
    /// a refusal or an explicit End ends the seat here and now.
    /// </summary>
    private void GroupParticipantDeparted(BoltGroupDeparture departure)
    {
        if (departure.Reason == BoltGroupDepartureReason.Disconnected) return;
        GroupMember[] cancel;
        lock (gate)
        {
            if (!groups.TryGetValue(departure.CallId, out var room)) return;
            var credential = room.Members.Keys.FirstOrDefault(x => ClientId(departure.CallId, x) == departure.ClientId);
            if (credential == Guid.Empty) return;
            var removed = room.Members[credential];
            if (removed.Left) return;
            // A connection this seat has since replaced: removing it was the point, not a refusal of the person.
            if (departure.Participant is { } principal && !IsCurrentConnection(principal, removed)) return;
            var reason = departure.Reason == BoltGroupDepartureReason.Unauthorized ? "removed" : null;
            removed.Left = true; removed.Ready = false; removed.Registered = false; removed.Connected = false; removed.Video = false;
            removed.AwaySince = null; removed.HoldUntil = null;
            AdvanceGroupRoster(room);
            Publish(room.Tenant, credential, GroupEvent(room, credential, "group-roster", reason: reason));
            PublishGroupLocked(room, "group-roster", reason);
            RemoveGroupTickets(departure.CallId, credential);
            cancel = [removed];
            if (!CanContinueGroup(room))
            {
                RemoveGroupLocked(room);
                cancel = room.Members.Values.ToArray();
                foreach (var id in room.Members.Keys) { room.Members[id].Left = true; RemoveGroupTickets(departure.CallId, id); }
                PublishGroupLocked(room, "group-ended", reason);
            }
        }
        foreach (var member in cancel) member.Lifetime.Cancel();
    }

    private void PruneGroups()
    {
        GroupRoom[] expired;
        lock (gate)
        {
            expired = groups.Values.Where(x => x.Expires <= DateTimeOffset.UtcNow || (!x.Started && x.InviteExpires <= DateTimeOffset.UtcNow) || !CanContinueGroup(x)).ToArray();
            foreach (var room in expired)
            {
                RemoveGroupLocked(room);
                foreach (var id in room.Members.Keys) { room.Members[id].Left = true; RemoveGroupTickets(room.Id, id); }
                AdvanceGroupRoster(room);
                PublishGroupLocked(room, "group-ended");
            }
            foreach (var room in groups.Values.Where(x => x.InviteExpires <= DateTimeOffset.UtcNow))
            {
                var pending = room.Members.Where(x => !x.Value.Accepted && !x.Value.Left).ToArray();
                foreach (var (id, member) in pending)
                {
                    member.Left = true;
                    Publish(room.Tenant, id, GroupEvent(room, id, "group-ended"));
                }
                if (pending.Length > 0) PublishGroupLocked(room, "group-roster");
            }
        }
        foreach (var room in expired) foreach (var member in room.Members.Values) member.Lifetime.Cancel();
    }

    private static bool CanContinueGroup(GroupRoom room)
    {
        var accepted = room.Members.Values.Count(x => x.Accepted && !x.Left);
        return accepted >= 2 || accepted == 1 && room.InviteExpires > DateTimeOffset.UtcNow &&
            room.Members.Values.Any(x => !x.Accepted && !x.Left);
    }

    private void RequireGroupLifecycle()
    { RequireEnabled(); if (!groupLifecycleEnabled) throw new YapApiException(503, "Encrypted group calls are not available yet."); }
    private bool GroupMemberBusy(Guid tenant, Guid credential) => groups.Values.Any(x => x.Tenant == tenant && x.Members.TryGetValue(credential, out var member) && !member.Left);
    private GroupRoom GetGroup(Guid tenant, Guid user, Guid call) => groups.TryGetValue(call, out var room) && room.Tenant == tenant &&
        room.Expires > DateTimeOffset.UtcNow && room.Members.TryGetValue(user, out var member) && !member.Left
        ? room : throw new YapApiException(404, "This call has ended.");
    private static void RequireAccepted(GroupMember member, ClaimsPrincipal user)
    {
        if (!member.Accepted || member.Left || string.IsNullOrEmpty(member.Session) || member.Session != user.FindFirstValue(YapAuth.SessionClaim))
            throw new YapApiException(403, "Accept this call on this session before connecting.");
    }
    private void RemoveGroupTickets(Guid call, Guid credential)
    { foreach (var key in tickets.Where(x => x.Value.CallId == call && x.Value.User == credential).Select(x => x.Key).ToArray()) tickets.Remove(key); }
    private static YapGroupCall Snapshot(GroupRoom room) => new(room.Id, room.Thread, room.Caller, room.CallerName, room.Revision, room.InviteExpires,
        room.Members.Select(x => new YapGroupParticipant(x.Key, x.Value.DeviceId, x.Value.Accepted, x.Value.Ready, x.Value.Left, x.Value.Muted, x.Value.Video,
            Reconnecting: !x.Value.Left && x.Value.AwaySince is not null)).ToArray(), room.VideoRequested);
    private sealed class GroupRoom(Guid id, Guid tenant, Guid thread, Guid caller, string callerName, TimeSpan lifetime)
    {
        public Guid Id { get; } = id;
        public Guid Tenant { get; } = tenant;
        public Guid Thread { get; } = thread;
        public Guid Caller { get; } = caller;
        public string CallerName { get; } = callerName;
        public long Revision = 1;
        public DateTimeOffset InviteExpires { get; } = DateTimeOffset.UtcNow.Add(InviteLifetime);
        public DateTimeOffset Expires { get; } = DateTimeOffset.UtcNow.Add(lifetime);
        public Dictionary<Guid, GroupMember> Members { get; } = [];
        public Dictionary<(Guid Sender, Guid Recipient, string Kind), YapGroupControlEvent> Controls { get; } = [];
        public bool VideoRequested;
        public bool HadVideo;
        public bool Started;
        public DateTimeOffset? ConnectedAt;
    }
    private sealed class GroupMember
    {
        public string? Session;
        public Guid DeviceId;
        public bool Muted, Video;
        public DateTimeOffset ControlWindow;
        public int ControlCount;
        public bool Accepted, Connected, Registered, Ready, Left;
        /// <summary>The seat has had a registered connection at least once, so losing it is a reconnect, not a failed join.</summary>
        public bool EverRegistered;
        /// <summary>Incremented per accepted socket. A resume ticket names the generation it replaces.</summary>
        public int Generation;
        /// <summary>Set while the participant is away (connection lost, or resumed but not yet back in the relay).</summary>
        public DateTimeOffset? AwaySince;
        /// <summary>When a held seat is given up. Null while a connection holds it.</summary>
        public DateTimeOffset? HoldUntil;
        /// <summary>The current socket's own lifetime, so a resume can supersede exactly that socket.</summary>
        public CancellationTokenSource? Connection;
        public TaskCompletionSource? Closed;
        /// <summary>Cancelled when the participant leaves the call; every socket's lifetime is linked to it.</summary>
        public CancellationTokenSource Lifetime { get; } = new();
    }
}
