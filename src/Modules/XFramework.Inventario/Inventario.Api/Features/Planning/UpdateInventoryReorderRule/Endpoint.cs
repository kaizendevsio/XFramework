using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;

namespace Inventario.Api.Features.Planning.UpdateInventoryReorderRule;

public static class UpdateInventoryReorderRuleEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/inventario/reorder-rules/update", Tags = ["Inventario"], Summary = "Update InventoryReorderRule metadata")]
    public static Task<Result<InventoryReorderRule>> Handle(UpdateInventoryReorderRuleRequest request, InventoryPlanningService service, CancellationToken ct) =>
        service.UpdateInventoryReorderRuleAsync(request, ct);
}
