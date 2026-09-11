using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Warehouses;

namespace Inventario.Api.Features.Warehouses.UpdateWarehouse;

public static class UpdateWarehouseEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/inventario/warehouses/update", Tags = ["Inventario"], Summary = "Update Warehouse metadata")]
    public static Task<Result<Warehouse>> Handle(UpdateWarehouseRequest request, WarehouseService service, CancellationToken ct) =>
        service.UpdateWarehouseAsync(request, ct);
}
