using Audit.Api.Services;
using XFramework.Integration.Attributes;
namespace Audit.Api.Features.Events.Get;
public static class GetAuditEventsEndpoint
{
    [BoltHandler(RequiredServiceScopes = ["audit.read"],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["audit:view"],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/audit/events/detail", Tags = ["Audit"], RequireAuthorization = true)]
    public static Task<Result<AuditDetail>> Handle(GetAuditEventRequest request, AuditQueryService service, CancellationToken ct)
        => service.GetAsync(request, ct);
}
