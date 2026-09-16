using Notifications.Api.Services.Push;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Notifications.Api.Features.Notifications.Push.SetPresence;

public static class SetPushPresenceEndpoint
{
    [BoltHandler]
    [MapPost("/api/notifications/push/subscriptions/presence", Tags = ["Notifications"],
        Summary = "Refresh the signed-in browser's foreground lease")]
    public static Task<Result> Handle(SetPushPresenceRequest request, NotificationPushService push, CancellationToken ct) =>
        push.SetPresenceAsync(request, ct);
}
