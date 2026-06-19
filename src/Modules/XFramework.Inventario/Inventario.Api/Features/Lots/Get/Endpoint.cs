using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Lots;

namespace Inventario.Api.Features.Lots.Get;

public static class GetInventoryLotEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/lots/{id:guid}", Tags = ["Inventario Traceability"],
        Summary = "Get inventory lot",
        Description = "Gets a traceability lot or batch for the authenticated tenant.")]
    public static async Task<Result<InventoryLot>> Handle(
        GetInventoryLotRequest request,
        InventoryLotService lotService,
        CancellationToken ct)
    {
        return await lotService.GetLotAsync(request, ct);
    }
}
