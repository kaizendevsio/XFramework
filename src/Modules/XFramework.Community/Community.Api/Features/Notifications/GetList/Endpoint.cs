using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using Community.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.Notifications.GetList;

public static class GetNotificationsEndpoint
{
    [BoltHandler]
    [MapGet("/api/community/notifications", Tags = ["Community Notifications"],
        Summary = "Get notifications for an identity",
        Description = "Returns a paginated list of notifications for a community identity, ordered by most recent. Optionally filtered by read status.")]
    public static async Task<Result<GetNotificationsResponse>> Handle(
        GetNotificationsRequest request,
        INotificationService notificationService,
        CancellationToken ct)
    {
        return await notificationService.GetNotificationsAsync(request, ct);
    }
}
