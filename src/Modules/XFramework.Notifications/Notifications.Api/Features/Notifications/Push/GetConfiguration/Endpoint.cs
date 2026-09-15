using Notifications.Api.Services.Push;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Notifications.Api.Features.Notifications.Push.GetConfiguration;

public static class GetPushConfigurationEndpoint
{
    [BoltHandler]
    [MapGet("/api/notifications/push/configuration", Tags = ["Notifications"],
        Summary = "Get web push configuration",
        Description = "Returns the VAPID application server key, or reports push as unavailable when no key pair is configured.")]
    public static Task<Result<PushConfigurationResponse>> Handle(
        GetPushConfigurationRequest request,
        NotificationPushService push,
        CancellationToken ct) =>
        push.GetConfigurationAsync(request, ct);
}
