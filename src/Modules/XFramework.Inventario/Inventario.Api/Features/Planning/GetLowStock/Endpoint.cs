using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace Inventario.Api.Features.Planning.GetLowStock;

public static class GetPlanningLowStockEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/planning/low-stock", Tags = ["Inventario Planning"],
        Summary = "Get low-stock planning rows",
        Description = "Returns products and scoped stock positions at or below active reorder thresholds.")]
    public static async Task<Result<List<LowStockReportRow>>> Handle(
        GetLowStockReportRequest request,
        InventoryPlanningService planningService,
        CancellationToken ct)
    {
        return await planningService.GetLowStockAsync(request, ct);
    }
}
