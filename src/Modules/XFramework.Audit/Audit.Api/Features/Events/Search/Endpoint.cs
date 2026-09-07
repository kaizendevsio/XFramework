using Audit.Api.Services;
using XFramework.Integration.Attributes;
namespace Audit.Api.Features.Events.Search;
public static class SearchAuditEventsEndpoint
{
    [BoltHandler(RequiredServiceScopes = ["audit.read"],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["audit:view"],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/audit/events/query", Tags = ["Audit"], RequireAuthorization = true)]
    public static Task<Result<AuditPage>> Handle(SearchAuditEventsRequest request, AuditQueryService service, CancellationToken ct)
        => service.SearchAsync(request, ct);
}
