using Notifications.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Notifications.Api.Features.Notifications.MarkRead;

public static class MarkNotificationReadEndpoint
{
    [BoltHandler]
    [MapPatch("/api/notifications/inbox/read", Tags = ["Notifications"],
        Summary = "Mark notifications read",
        Description = "Marks notification inbox items read for the requesting credential.")]
    public static Task<Result> Handle(
        MarkNotificationReadRequest request,
        NotificationService notificationService,
        CancellationToken ct) =>
        notificationService.MarkReadAsync(request, ct);
}
