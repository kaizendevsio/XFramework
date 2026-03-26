using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.Notifications.MarkRead;

public static class MarkNotificationsReadEndpoint
{
    [BoltHandler]
    [MapPost("/api/community/notifications/read", Tags = ["Community Notifications"],
        Summary = "Mark notifications as read",
        Description = "Marks one or more notifications as read by their IDs.")]
    public static async Task<Result<CmdResponse>> Handle(
        MarkNotificationsReadRequest request,
        INotificationService notificationService,
        CancellationToken ct)
    {
        return await notificationService.MarkNotificationsReadAsync(request, ct);
    }
}
