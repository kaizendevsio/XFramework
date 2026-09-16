using Communications.Domain.Shared.Contracts.Requests.Threads;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Messages.RecordCall;

public static class RecordCallEndpoint
{
    [BoltHandler(ActorRequirement = ActorRequirement.None,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.Yap])]
    [MapPost("/api/communications/calls/history", Tags = ["Messages"], Summary = "Record a server-observed call outcome")]
    public static Task<Result> Handle(RecordCallRequest request, IThreadService service, CancellationToken ct)
        => service.RecordCallAsync(request, ct);
}
