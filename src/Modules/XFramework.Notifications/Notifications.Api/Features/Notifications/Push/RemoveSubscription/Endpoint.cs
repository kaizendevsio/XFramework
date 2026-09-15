using Notifications.Api.Services.Push;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Notifications.Api.Features.Notifications.Push.RemoveSubscription;

public static class RemovePushSubscriptionEndpoint
{
    [BoltHandler]
    [MapPost("/api/notifications/push/subscriptions/remove", Tags = ["Notifications"],
        Summary = "Remove a web push subscription",
        Description = "Deletes one stored Push API endpoint for the signed-in credential. Idempotent.")]
    public static Task<Result> Handle(
        RemovePushSubscriptionRequest request,
        NotificationPushService push,
        CancellationToken ct) =>
        push.RemoveAsync(request, ct);
}
