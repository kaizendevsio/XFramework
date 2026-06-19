using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;

namespace Inventario.Api.Features.Planning.CreateReorderRule;

public static class CreateInventoryReorderRuleEndpoint
{
    [BoltHandler]
    [MapPost("/api/inventario/reorder-rules", Tags = ["Inventario Planning"],
        Summary = "Create inventory reorder rule",
        Description = "Creates a tenant-scoped reorder rule for product, warehouse, or location planning.")]
    public static async Task<Result<InventoryReorderRule>> Handle(
        CreateInventoryReorderRuleRequest request,
        InventoryPlanningService planningService,
        CancellationToken ct)
    {
        return await planningService.CreateRuleAsync(request, ct);
    }
}
