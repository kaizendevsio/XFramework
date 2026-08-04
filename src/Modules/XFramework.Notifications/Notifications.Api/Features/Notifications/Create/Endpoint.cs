using Notifications.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Notifications.Api.Features.Notifications.Create;

public static class CreateNotificationEndpoint
{
    [BoltHandler(
        ActorRequirement = ActorRequirement.None,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.NotificationsSend, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.Communications])]
    [MapPost("/api/notifications", Tags = ["Notifications"],
        Summary = "Create notification",
        Description = "Creates a tenant-scoped notification inbox item for a credential.")]
    public static Task<Result<NotificationInboxItemResponse>> Handle(
        CreateNotificationRequest request,
        NotificationService notificationService,
        CancellationToken ct) =>
        notificationService.CreateNotificationAsync(request, ct);
}
