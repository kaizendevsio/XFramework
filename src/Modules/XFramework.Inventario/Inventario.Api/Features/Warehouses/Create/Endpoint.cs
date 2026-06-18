using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Warehouses;

namespace Inventario.Api.Features.Warehouses.Create;

public static class CreateWarehouseEndpoint
{
    [BoltHandler]
    [MapPost("/api/inventario/warehouses", Tags = ["Inventario Warehousing"],
        Summary = "Create warehouse",
        Description = "Creates a warehouse for the authenticated tenant.")]
    public static async Task<Result<Warehouse>> Handle(
        CreateWarehouseRequest request,
        WarehouseService warehouseService,
        CancellationToken ct)
    {
        return await warehouseService.CreateWarehouseAsync(request, ct);
    }
}
