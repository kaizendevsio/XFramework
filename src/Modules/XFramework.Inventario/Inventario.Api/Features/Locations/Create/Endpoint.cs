using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Locations;

namespace Inventario.Api.Features.Locations.Create;

public static class CreateInventoryLocationEndpoint
{
    [BoltHandler]
    [MapPost("/api/inventario/locations", Tags = ["Inventario Warehousing"],
        Summary = "Create inventory location",
        Description = "Creates an inventory location for a warehouse.")]
    public static async Task<Result<InventoryLocation>> Handle(
        CreateInventoryLocationRequest request,
        WarehouseService warehouseService,
        CancellationToken ct)
    {
        return await warehouseService.CreateLocationAsync(request, ct);
    }
}
