using Notifications.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Notifications.Api.Features.Notifications.RecordDeliveryStatus;

public static class RecordNotificationDeliveryStatusEndpoint
{
    [BoltHandler]
    [MapPost("/api/notifications/delivery-status", Tags = ["Notifications"],
        Summary = "Record notification delivery status",
        Description = "Records or advances delivery status for a notification and channel.")]
    public static Task<Result<NotificationDeliveryStatusResponse>> Handle(
        RecordNotificationDeliveryStatusRequest request,
        NotificationService notificationService,
        CancellationToken ct) =>
        notificationService.RecordDeliveryStatusAsync(request, ct);
}
