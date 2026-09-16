using Notifications.Domain.Shared.Contracts.Requests;
using Notifications.Integration.Drivers;

namespace Yap.Services;

public sealed partial class YapCallGateway
{
    /// <summary>
    /// Rings a device that is not running the app.
    ///
    /// Call invitations never reach the Communications outbox - this gateway is in-memory - so the
    /// 5s/15s delivery pollers would surface a "missed call" long after the 60 second invite
    /// expired. The push therefore goes straight to the Notifications module over Bolt, with a TTL
    /// matched to the invite so a push service never wakes a phone for a call that is already over.
    ///
    /// Fire and forget on purpose: the caller's HTTP request must not wait on a push service, and a
    /// push failure must never fail the call.
    /// </summary>
    private void NotifyIncomingCall(Guid tenant, Guid thread, Guid callId, DateTimeOffset expires, IReadOnlyCollection<Guid> recipients)
    {
        if (recipients.Count == 0) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationsServiceWrapper>();
                foreach (var recipient in recipients)
                {
                    // Recomputed per recipient rather than once before queuing: scope creation, the
                    // Bolt round trip and a slow push service all burn invite time, and a TTL
                    // measured before any of that would let a push service hold the ring past the
                    // deadline this gateway enforces.
                    var deadline = RemainingSeconds(expires, DateTimeOffset.UtcNow);
                    if (deadline == 0) break; // Nothing left to ring for; the invite has already timed out.
                    await notifications.SendDirectPush(new SendDirectPushRequest
                    {
                        TenantId = tenant,
                        RecipientCredentialId = recipient,
                        Kind = "call",
                        ThreadId = thread,
                        // The call ID lets the service worker replace a ringing notification and
                        // close it when the invite is answered elsewhere or expires.
                        Reference = callId.ToString("N"),
                        TimeToLiveSeconds = deadline,
                        // A TTL only bounds delivery attempts; a push held to the last second still
                        // arrives. The absolute deadline lets the worker render a missed call
                        // instead of an invitation the authenticated flow would refuse.
                        ExpiresAt = expires,
                        Urgency = "high",
                        Metadata = YapPush.Metadata(tenant)
                    });
                }
            }
            catch (Exception ex)
            {
                // No call ID, thread or recipient is logged: a wake-up failure is operational noise.
                pushLogger.LogDebug(ex, "Incoming-call push could not be delivered");
            }
        });
    }

    /// <summary>
    /// Seconds a ring is still worth delivering. Floored, never rounded up, so the TTL can only
    /// ever expire before the invite does.
    /// </summary>
    internal static int RemainingSeconds(DateTimeOffset expires, DateTimeOffset now) =>
        (int)Math.Clamp(Math.Floor((expires - now).TotalSeconds), 0, int.MaxValue);
}
