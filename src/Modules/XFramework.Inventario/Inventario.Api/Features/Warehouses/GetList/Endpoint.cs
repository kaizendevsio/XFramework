using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Warehouses;

namespace Inventario.Api.Features.Warehouses.GetList;

public static class GetWarehousesEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/warehouses", Tags = ["Inventario Warehousing"],
        Summary = "Get warehouses",
        Description = "Gets warehouses for the authenticated tenant.")]
    public static async Task<Result<List<Warehouse>>> Handle(
        GetWarehousesRequest request,
        WarehouseService warehouseService,
        CancellationToken ct)
    {
        return await warehouseService.GetWarehousesAsync(request, ct);
    }
}
