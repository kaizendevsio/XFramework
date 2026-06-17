using Notifications.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Notifications.Api.Features.Notifications.GetInbox;

public static class GetNotificationInboxEndpoint
{
    [BoltHandler]
    [MapGet("/api/notifications/inbox", Tags = ["Notifications"],
        Summary = "Get notification inbox",
        Description = "Retrieves tenant-scoped notification inbox items for a credential.")]
    public static Task<Result<GetNotificationInboxResponse>> Handle(
        GetNotificationInboxRequest request,
        NotificationService notificationService,
        CancellationToken ct) =>
        notificationService.GetInboxAsync(request, ct);
}
