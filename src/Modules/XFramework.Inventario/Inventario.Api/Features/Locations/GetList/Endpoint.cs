using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Locations;

namespace Inventario.Api.Features.Locations.GetList;

public static class GetInventoryLocationsEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/locations", Tags = ["Inventario Warehousing"],
        Summary = "Get inventory locations",
        Description = "Gets inventory locations for the authenticated tenant.")]
    public static async Task<Result<List<InventoryLocation>>> Handle(
        GetInventoryLocationsRequest request,
        WarehouseService warehouseService,
        CancellationToken ct)
    {
        return await warehouseService.GetLocationsAsync(request, ct);
    }
}
