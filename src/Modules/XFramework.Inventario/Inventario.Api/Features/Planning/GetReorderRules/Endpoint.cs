using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;

namespace Inventario.Api.Features.Planning.GetReorderRules;

public static class GetInventoryReorderRulesEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/reorder-rules", Tags = ["Inventario Planning"],
        Summary = "Get inventory reorder rules",
        Description = "Gets active or inactive reorder rules for the authenticated tenant.")]
    public static async Task<Result<List<InventoryReorderRule>>> Handle(
        GetInventoryReorderRulesRequest request,
        InventoryPlanningService planningService,
        CancellationToken ct)
    {
        return await planningService.GetRulesAsync(request, ct);
    }
}
