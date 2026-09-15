using Notifications.Api.Services.Push;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Notifications.Api.Features.Notifications.Push.SendDirect;

public static class SendDirectPushEndpoint
{
    // Ringing calls never reach the Communications outbox and cannot wait for the delivery poller,
    // so a trusted presentation host sends the wake-up itself. The payload is routing-only, which
    // is why this does not go through the inbox pipeline.
    [BoltHandler(
        ActorRequirement = ActorRequirement.None,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.NotificationsSend, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.Communications, XFrameworkServiceNames.Yap])]
    [MapPost("/api/notifications/push/send", Tags = ["Notifications"],
        Summary = "Send an immediate web push",
        Description = "Delivers a routing-only wake-up to every registered device for a credential.")]
    public static Task<Result<SendDirectPushResponse>> Handle(
        SendDirectPushRequest request,
        NotificationPushService push,
        CancellationToken ct) =>
        push.SendDirectAsync(request, ct);
}
