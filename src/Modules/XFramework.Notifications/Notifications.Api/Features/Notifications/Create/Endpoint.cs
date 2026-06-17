using Notifications.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Notifications.Api.Features.Notifications.Create;

public static class CreateNotificationEndpoint
{
    [BoltHandler]
    [MapPost("/api/notifications", Tags = ["Notifications"],
        Summary = "Create notification",
        Description = "Creates a tenant-scoped notification inbox item for a credential.")]
    public static Task<Result<NotificationInboxItemResponse>> Handle(
        CreateNotificationRequest request,
        NotificationService notificationService,
        CancellationToken ct) =>
        notificationService.CreateNotificationAsync(request, ct);
}
