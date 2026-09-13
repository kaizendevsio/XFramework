using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Security.Cryptography;
using Bolt.Protocol.Transport;

[assembly: InternalsVisibleTo("Yap.Tests")]

namespace Yap.Services;

public sealed partial class YapCallGateway
{
    private readonly bool groupLifecycleEnabled;
    private readonly Dictionary<Guid, GroupRoom> groups = [];

    // Deliberately not exposed by MapYapCalls. The production constructor disables admission.
    internal async Task<GroupSnapshot> StartGroupAsync(ClaimsPrincipal user, Guid thread, IReadOnlyCollection<Guid> recipients, CancellationToken ct = default)
    {
        RequireGroupLifecycle();
        var (tenant, caller) = Identity(user);
        if (thread == Guid.Empty || recipients.Count is < 2 or > 7 || recipients.Contains(Guid.Empty) ||
            recipients.Contains(caller) || recipients.Distinct().Count() != recipients.Count)
            throw new YapApiException(400, "Choose between two and seven other conversation members.");
        var members = await CurrentMembersAsync(user, thread, ct);
        if (recipients.Any(x => !members.Contains(x))) throw new YapApiException(403, "Only current conversation members can join.");
        var room = new GroupRoom(Guid.NewGuid(), tenant, thread);
        room.Members.Add(caller, new GroupMember { Session = user.FindFirstValue(YapAuth.SessionClaim), Accepted = true });
        foreach (var id in recipients) room.Members.Add(id, new GroupMember());
        lock (gate)
        {
            if (groups.Count + invites.Count >= 64 || room.Members.Keys.Any(id => GroupMemberBusy(tenant, id) ||
                invites.Values.Any(x => x.Tenant == tenant && (x.Invite.CallerId == id || x.Invite.RecipientId == id))))
                throw new YapApiException(409, "Someone is already in a call.");
            groups.Add(room.Id, room);
            return Snapshot(room);
        }
    }

    internal async Task<GroupSnapshot> AcceptGroupAsync(ClaimsPrincipal user, Guid callId, CancellationToken ct = default)
    {
        RequireGroupLifecycle();
        var (tenant, credential) = Identity(user);
        GroupRoom room;
        lock (gate) room = GetGroup(tenant, credential, callId);
        await VerifyMembershipAsync(user, room.Thread, credential, ct);
        lock (gate)
        {
            room = GetGroup(tenant, credential, callId);
            var member = room.Members[credential];
            var session = user.FindFirstValue(YapAuth.SessionClaim);
            if (member.Accepted && member.Session != session) throw new YapApiException(409, "This call is already accepted on another session.");
            if (!member.Accepted && room.InviteExpires <= DateTimeOffset.UtcNow) throw new YapApiException(410, "This invitation expired.");
            member.Accepted = true; member.Session = session;
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
        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        lock (gate)
        {
            room = GetGroup(tenant, credential, callId);
            var member = room.Members[credential];
            RequireAccepted(member, user);
            if (member.Connected) throw new YapApiException(409, "This call is already connected.");
            RemoveGroupTickets(callId, credential);
            tickets[ticket] = new(callId, tenant, credential, member.Session!, DateTimeOffset.UtcNow.AddSeconds(30), Group: true);
        }
        // A host-managed group has no single recipient. The group client obtains its accepted roster separately.
        return new(callId, $"/api/chat/calls/socket?ticket={ticket}", ClientId(callId, credential), "");
    }

    internal async Task ReadyGroupAsync(ClaimsPrincipal user, Guid callId, CancellationToken ct = default)
    {
        var (tenant, credential) = Identity(user);
        GroupRoom room;
        lock (gate)
        {
            room = GetGroup(tenant, credential, callId);
            var member = room.Members[credential];
            RequireAccepted(member, user);
            if (!member.Registered || !member.Connected) throw new YapApiException(409, "Connect before joining this call.");
        }
        if (!await Server.JoinGroupCallAsync(callId, ClientId(callId, credential), ct))
        {
            await RemoveGroupMemberAsync(room, credential, ct);
            throw new YapApiException(403, "This call is no longer available.");
        }
        lock (gate)
        {
            room = GetGroup(tenant, credential, callId);
            room.Members[credential].Ready = true;
            if (room.Members.Values.Count(x => x.Ready && !x.Left) >= 2) room.Started = true;
        }
    }

    internal async Task LeaveGroupAsync(ClaimsPrincipal user, Guid callId, CancellationToken ct = default)
    {
        var (tenant, credential) = Identity(user);
        GroupRoom room;
        lock (gate) room = GetGroup(tenant, credential, callId);
        await RemoveGroupMemberAsync(room, credential, ct);
    }

    internal GroupSnapshot GroupRoster(ClaimsPrincipal user, Guid callId)
    {
        var (tenant, credential) = Identity(user);
        lock (gate) return Snapshot(GetGroup(tenant, credential, callId));
    }

    public async ValueTask<bool> AuthorizeParticipantAsync(Guid callId, string clientId, ClaimsPrincipal participant, CancellationToken ct = default)
    {
        if (!groupLifecycleEnabled || participant.Identity?.IsAuthenticated != true) return false;
        var (tenant, credential) = Identity(participant);
        GroupRoom room;
        lock (gate)
        {
            if (!groups.TryGetValue(callId, out room!) || room.Tenant != tenant || room.Expires <= DateTimeOffset.UtcNow ||
                !room.Members.TryGetValue(credential, out var member) || member.Left || !member.Accepted || !member.Registered ||
                !member.Connected || member.Session != participant.FindFirstValue(YapAuth.SessionClaim) ||
                clientId != ClientId(callId, credential) || participant.FindFirstValue("bolt_media_client_id") != clientId ||
                participant.FindFirstValue("yap_call_id") != callId.ToString()) return false;
        }
        await VerifyMembershipAsync(participant, room.Thread, credential, ct);
        lock (gate) return groups.TryGetValue(callId, out var current) && ReferenceEquals(room, current) && room.Expires > DateTimeOffset.UtcNow &&
            room.Members.TryGetValue(credential, out var member) && !member.Left && member.Connected && member.Registered && member.Accepted &&
            member.Session == participant.FindFirstValue(YapAuth.SessionClaim);
    }

    private async Task AcceptGroupSocketAsync(HttpContext context, string token)
    {
        RequireGroupLifecycle();
        var (tenant, credential) = Identity(context.User);
        GroupRoom room;
        GroupMember member;
        lock (gate)
        {
            if (!tickets.TryGetValue(token, out var ticket) || !ticket.Group || ticket.ExpiresAt <= DateTimeOffset.UtcNow ||
                ticket.Tenant != tenant || ticket.User != credential || ticket.Session != context.User.FindFirstValue(YapAuth.SessionClaim))
                throw new YapApiException(403, "This call connection has expired.");
            room = GetGroup(tenant, credential, ticket.CallId);
            member = room.Members[credential];
            RequireAccepted(member, context.User);
            tickets.Remove(token);
            if (member.Connected) throw new YapApiException(409, "This call is already connected.");
            member.Connected = true;
        }
        try
        {
            await VerifyMembershipAsync(context.User, room.Thread, credential, context.RequestAborted);
            lock (gate) { GetGroup(tenant, credential, room.Id); RequireAccepted(member, context.User); }
            var identity = new ClaimsIdentity(context.User.Identity as ClaimsIdentity);
            identity.AddClaim(new("bolt_media_client_id", ClientId(room.Id, credential)));
            identity.AddClaim(new("yap_call_id", room.Id.ToString()));
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, member.Lifetime.Token);
            lifetime.CancelAfter(TimeSpan.FromHours(1));
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await using var transport = new ReadyTransport(new WebSocketBoltConnection(socket), () => { lock (gate) member.Registered = !member.Left; });
            await Server.HandleConnectionAsync(transport, new ClaimsPrincipal(identity), lifetime.Token, isSecureTransport: true);
        }
        finally { await RemoveGroupMemberAsync(room, credential, CancellationToken.None); }
    }

    private async Task RemoveGroupMemberAsync(GroupRoom room, Guid credential, CancellationToken ct)
    {
        GroupMember[] cancel;
        lock (gate)
        {
            if (!room.Members.TryGetValue(credential, out var member)) return;
            member.Left = true; member.Ready = false; member.Registered = false; member.Connected = false;
            RemoveGroupTickets(room.Id, credential);
            cancel = [member];
            if (!room.Members.Values.Any(x => x.Accepted && !x.Left))
            {
                groups.Remove(room.Id);
                cancel = room.Members.Values.ToArray();
                foreach (var id in room.Members.Keys) RemoveGroupTickets(room.Id, id);
            }
        }
        foreach (var member in cancel) member.Lifetime.Cancel();
        await Server.LeaveGroupCallAsync(room.Id, ClientId(room.Id, credential), ct);
    }

    private void GroupParticipantRemoved(Guid call, string clientId)
    {
        GroupMember[] cancel;
        lock (gate)
        {
            if (!groups.TryGetValue(call, out var room)) return;
            var credential = room.Members.Keys.FirstOrDefault(x => ClientId(call, x) == clientId);
            if (credential == Guid.Empty) return;
            var removed = room.Members[credential];
            removed.Left = true; removed.Ready = false; removed.Registered = false; removed.Connected = false;
            RemoveGroupTickets(call, credential);
            cancel = [removed];
            if (!room.Members.Values.Any(x => x.Accepted && !x.Left))
            {
                groups.Remove(call);
                cancel = room.Members.Values.ToArray();
                foreach (var id in room.Members.Keys) RemoveGroupTickets(call, id);
            }
        }
        foreach (var member in cancel) member.Lifetime.Cancel();
    }

    private void PruneGroups()
    {
        GroupRoom[] expired;
        lock (gate)
        {
            expired = groups.Values.Where(x => x.Expires <= DateTimeOffset.UtcNow || (!x.Started && x.InviteExpires <= DateTimeOffset.UtcNow)).ToArray();
            foreach (var room in expired)
            {
                groups.Remove(room.Id);
                foreach (var id in room.Members.Keys) { room.Members[id].Left = true; RemoveGroupTickets(room.Id, id); }
            }
        }
        foreach (var room in expired) foreach (var member in room.Members.Values) member.Lifetime.Cancel();
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
    private static GroupSnapshot Snapshot(GroupRoom room) => new(room.Id, room.Thread,
        room.Members.Select(x => new GroupParticipant(x.Key, x.Value.Accepted, x.Value.Ready, x.Value.Left)).ToArray());
    internal sealed record GroupSnapshot(Guid Id, Guid ThreadId, GroupParticipant[] Participants);
    internal sealed record GroupParticipant(Guid CredentialId, bool Accepted, bool Ready, bool Left);
    private sealed class GroupRoom(Guid id, Guid tenant, Guid thread)
    {
        public Guid Id { get; } = id;
        public Guid Tenant { get; } = tenant;
        public Guid Thread { get; } = thread;
        public DateTimeOffset InviteExpires { get; } = DateTimeOffset.UtcNow.AddSeconds(60);
        public DateTimeOffset Expires { get; } = DateTimeOffset.UtcNow.AddHours(1);
        public Dictionary<Guid, GroupMember> Members { get; } = [];
        public bool Started;
    }
    private sealed class GroupMember
    {
        public string? Session;
        public bool Accepted, Connected, Registered, Ready, Left;
        public CancellationTokenSource Lifetime { get; } = new();
    }
}
