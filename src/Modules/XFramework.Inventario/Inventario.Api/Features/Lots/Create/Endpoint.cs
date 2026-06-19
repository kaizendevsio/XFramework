using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Lots;

namespace Inventario.Api.Features.Lots.Create;

public static class CreateInventoryLotEndpoint
{
    [BoltHandler]
    [MapPost("/api/inventario/lots", Tags = ["Inventario Traceability"],
        Summary = "Create inventory lot",
        Description = "Creates a traceability lot or batch for the authenticated tenant.")]
    public static async Task<Result<InventoryLot>> Handle(
        CreateInventoryLotRequest request,
        InventoryLotService lotService,
        CancellationToken ct)
    {
        return await lotService.CreateLotAsync(request, ct);
    }
}
