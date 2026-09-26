using System.Security.Claims;
using System.Security.Cryptography;
using Bolt.Server;
using Yap.Contracts;

namespace Yap.Services;

/// <summary>
/// Resumable calls: a participant whose connection dropped keeps the seat for
/// <see cref="ReconnectGrace"/> and comes back on a new socket with a resume ticket.
///
/// A resume ticket is a fresh 256-bit random value, issued only to the session that accepted the seat,
/// on the device the seat was accepted with, after membership and device are checked again. It is
/// single use, valid for 30 seconds, stored only as a hash, and names the connection generation it
/// replaces: issuing another one voids it, a completed resume voids it, and leaving the call or the call
/// ending voids it. It is never the join ticket, which is spent by the first connection.
/// </summary>
public sealed partial class YapCallGateway
{
    internal async Task<YapCallConnection> ResumeGroupAsync(ClaimsPrincipal user, Guid callId, Guid deviceId, CancellationToken ct = default)
    {
        RequireGroupLifecycle();
        var (tenant, credential) = Identity(user);
        GroupRoom room;
        lock (gate)
        {
            room = GetGroup(tenant, credential, callId);
            RequireResumable(room.Members[credential], user, deviceId);
        }

        // The same checks as joining, classified like the relay's re-check: a refusal ends the seat, an
        // answer that cannot be given right now only means "not yet" and the phone tries again.
        BoltGroupAuthorizationDecision decision;
        try
        {
            decision = await CheckMembershipAsync(user, room.Thread, credential, ct);
            if (decision == BoltGroupAuthorizationDecision.Allowed) decision = await CheckCallDeviceAsync(user, deviceId, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested) { decision = Classify(error); }
        if (decision == BoltGroupAuthorizationDecision.Denied)
        {
            await RemoveGroupMemberAsync(room, credential, CancellationToken.None, "removed");
            throw new YapApiException(403, "You can no longer join this call.");
        }
        if (decision != BoltGroupAuthorizationDecision.Allowed) throw new YapApiException(503, "The call could not be resumed yet. Try again.");

        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        lock (gate)
        {
            room = GetGroup(tenant, credential, callId);
            var member = room.Members[credential];
            RequireResumable(member, user, deviceId);
            // Rotation: exactly one outstanding admission per seat, and it is this one.
            RemoveGroupTickets(callId, credential);
            var expires = DateTimeOffset.UtcNow.AddSeconds(30);
            if (member.HoldUntil is { } until && until < expires) expires = until;
            tickets[TicketKey(ticket)] = new(callId, tenant, credential, member.Session!, expires, Group: true,
                Resume: true, DeviceId: member.DeviceId, Generation: member.Generation);
        }
        return new(callId, $"/api/chat/calls/socket?ticket={ticket}", ClientId(callId, credential), "");
    }

    private static void RequireResumable(GroupMember member, ClaimsPrincipal user, Guid deviceId)
    {
        RequireAccepted(member, user);
        if (!member.EverRegistered) throw new YapApiException(409, "Connect to this call first.");
        if (member.DeviceId != deviceId) throw new YapApiException(403, "This call belongs to another device.");
    }
}
