using System.Security.Claims;
using System.Text;
using Yap.Contracts;

namespace Yap.Services;

public sealed partial class YapCallGateway
{
    internal async Task RelayGroupControlAsync(ClaimsPrincipal user, Guid callId, YapGroupControl request, CancellationToken ct = default)
    {
        RequireGroupLifecycle();
        var (tenant, sender) = Identity(user);
        if (request.Kind is not ("key" or "ack") || request.Sequence <= 0 || request.RecipientId == sender || request.RecipientId == Guid.Empty ||
            string.IsNullOrEmpty(request.Envelope) || request.Envelope.Length > 32768 || Encoding.UTF8.GetByteCount(request.Envelope) > 32768)
            throw new YapApiException(400, "Invalid encrypted call control.");
        GroupRoom room;
        lock (gate) { room = GetGroup(tenant, sender, callId); RequireAccepted(room.Members[sender], user); }
        var members = await CurrentMembersAsync(user, room.Thread, ct);
        Guid recipientDevice;
        lock (gate)
        {
            room = GetGroup(tenant, sender, callId);
            if (!room.Members.TryGetValue(request.RecipientId, out var member) || !member.Accepted || member.Left)
                throw new YapApiException(403, "Only accepted conversation members can exchange call keys.");
            recipientDevice = member.DeviceId;
        }
        await VerifyCallDeviceAsync(user, room.Members[sender].DeviceId, ct);
        await VerifyCallDeviceAsync(user, recipientDevice, ct, request.RecipientId);
        lock (gate)
        {
            room = GetGroup(tenant, sender, callId);
            var member = room.Members[sender];
            RequireAccepted(member, user);
            if (!members.Contains(sender) || !members.Contains(request.RecipientId) ||
                !room.Members.TryGetValue(request.RecipientId, out var recipient) || recipient.Left || !recipient.Accepted)
                throw new YapApiException(403, "Only accepted conversation members can exchange call keys.");
            if (request.Revision != room.Revision) throw new YapApiException(409, "The call membership changed.");
            var pair = (sender, request.RecipientId, request.Kind);
            if (room.Controls.TryGetValue(pair, out var previous) && request.Sequence <= previous.Sequence)
                throw new YapApiException(409, "This call control was already received.");
            var now = DateTimeOffset.UtcNow;
            if (now - member.ControlWindow > TimeSpan.FromSeconds(1)) { member.ControlWindow = now; member.ControlCount = 0; }
            if (++member.ControlCount > 32) throw new YapApiException(429, "Too many call controls. Try again shortly.");
            var control = new YapGroupControlEvent(callId, room.Revision, request.Sequence, sender, member.DeviceId, request.RecipientId, request.Envelope, request.Kind);
            // Retain key and acknowledgment independently for reconnect replay; discard on roster changes.
            room.Controls[pair] = control;
            Publish(room.Tenant, request.RecipientId, GroupEvent(room, request.RecipientId, "group-control", control));
        }
    }

    internal void MuteGroup(ClaimsPrincipal user, Guid callId, bool muted)
    {
        RequireGroupLifecycle();
        var (tenant, credential) = Identity(user);
        lock (gate)
        {
            var room = GetGroup(tenant, credential, callId);
            RequireAccepted(room.Members[credential], user);
            room.Members[credential].Muted = muted;
            PublishGroupLocked(room, "group-roster");
        }
    }

    /// <summary>Camera on/off, published to the roster so the other screens can lay out their tiles.</summary>
    internal void VideoGroup(ClaimsPrincipal user, Guid callId, bool video)
    {
        RequireGroupLifecycle();
        if (video && !videoEnabled) throw new YapApiException(503, "Video calls are not available yet.");
        var (tenant, credential) = Identity(user);
        lock (gate)
        {
            var room = GetGroup(tenant, credential, callId);
            RequireAccepted(room.Members[credential], user);
            // More cameras than any phone can decode is refused here, not negotiated between clients.
            if (video && room.Members.Count(x => x.Value.Video && !x.Value.Left && x.Key != credential) >= MaxVideoSenders)
                throw new YapApiException(409, "This call already has as many cameras as it can carry.");
            room.Members[credential].Video = video;
            room.HadVideo |= video;
            PublishGroupLocked(room, "group-roster");
        }
    }

    private static void AdvanceGroupRoster(GroupRoom room)
    {
        room.Revision++;
        room.Controls.Clear();
        foreach (var member in room.Members.Values) member.Ready = false;
    }

    private static YapCallEvent GroupEvent(GroupRoom room, Guid recipient, string type, YapGroupControlEvent? control = null, string? reason = null) =>
        new(type, new(room.Id, room.Thread, room.Caller, room.CallerName, recipient, room.InviteExpires),
            control?.SenderId, Snapshot(room), control, reason);

    private void PublishGroupLocked(GroupRoom room, string type, string? reason = null)
    {
        foreach (var recipient in room.Members.Where(x => !x.Value.Left || type == "group-ended").Select(x => x.Key))
            Publish(room.Tenant, recipient, GroupEvent(room, recipient, type, reason: reason));
    }

    private void ReplayGroupsLocked(Guid tenant, Guid credential, Action<YapCallEvent> handler)
    {
        foreach (var room in groups.Values.Where(x => x.Tenant == tenant && x.Expires > DateTimeOffset.UtcNow &&
                     x.Members.TryGetValue(credential, out var member) && !member.Left))
        {
            var self = room.Members[credential];
            if (!self.Accepted && room.InviteExpires <= DateTimeOffset.UtcNow) continue;
            handler(GroupEvent(room, credential, self.Accepted ? "group-roster" : "group-incoming"));
            if (self.Accepted)
                foreach (var control in room.Controls.Values.Where(x => x.RecipientId == credential))
                    handler(GroupEvent(room, credential, "group-control", control));
        }
    }
}
