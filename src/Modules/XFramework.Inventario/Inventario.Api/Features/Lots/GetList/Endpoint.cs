using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Lots;

namespace Inventario.Api.Features.Lots.GetList;

public static class GetInventoryLotsEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/lots", Tags = ["Inventario Traceability"],
        Summary = "Get inventory lots",
        Description = "Gets traceability lots or batches for the authenticated tenant.")]
    public static async Task<Result<List<InventoryLot>>> Handle(
        GetInventoryLotsRequest request,
        InventoryLotService lotService,
        CancellationToken ct)
    {
        return await lotService.GetLotsAsync(request, ct);
    }
}
