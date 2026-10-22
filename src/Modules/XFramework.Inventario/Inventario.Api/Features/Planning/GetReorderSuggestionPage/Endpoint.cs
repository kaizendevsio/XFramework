using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace Inventario.Api.Features.Planning.GetReorderSuggestionPage;

public static class GetReorderSuggestionPageEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["inventario.planning:view"],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapGet("/api/inventario/planning/reorder-suggestions/page", Tags = ["Inventario Planning"],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["inventario.planning:view"],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    public static Task<Result<ReorderSuggestionPage>> Handle(GetReorderSuggestionPageRequest request,
        InventoryPlanningService planningService, CancellationToken ct) => planningService.GetReorderSuggestionPageAsync(request, ct);
}
