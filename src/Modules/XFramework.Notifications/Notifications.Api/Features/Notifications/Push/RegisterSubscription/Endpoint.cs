using Notifications.Api.Services.Push;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Notifications.Api.Features.Notifications.Push.RegisterSubscription;

public static class RegisterPushSubscriptionEndpoint
{
    [BoltHandler]
    [MapPost("/api/notifications/push/subscriptions", Tags = ["Notifications"],
        Summary = "Register a web push subscription",
        Description = "Stores one browser Push API endpoint for the signed-in credential's device.")]
    public static Task<Result<PushSubscriptionResponse>> Handle(
        RegisterPushSubscriptionRequest request,
        NotificationPushService push,
        CancellationToken ct) =>
        push.RegisterAsync(request, ct);
}
