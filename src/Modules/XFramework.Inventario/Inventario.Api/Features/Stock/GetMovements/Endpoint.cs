using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;

namespace Inventario.Api.Features.Stock.GetMovements;

public static class GetInventoryMovementsEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/stock/movements", Tags = ["Inventario Stock"],
        Summary = "Get inventory movement ledger",
        Description = "Gets append-only inventory movements for the authenticated tenant.")]
    public static async Task<Result<List<InventoryMovement>>> Handle(
        GetInventoryMovementsRequest request,
        StockPostingService stockPostingService,
        CancellationToken ct)
    {
        return await stockPostingService.GetMovementsAsync(request, ct);
    }
}
