using Notifications.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Notifications.Api.Features.Notifications.UpdatePreferences;

public static class UpdateNotificationPreferencesEndpoint
{
    [BoltHandler]
    [MapPut("/api/notifications/preferences", Tags = ["Notifications"],
        Summary = "Update notification preferences",
        Description = "Creates or updates delivery-channel and template preferences for a credential.")]
    public static Task<Result<NotificationPreferencesResponse>> Handle(
        UpdateNotificationPreferencesRequest request,
        NotificationService notificationService,
        CancellationToken ct) =>
        notificationService.UpdatePreferencesAsync(request, ct);
}
