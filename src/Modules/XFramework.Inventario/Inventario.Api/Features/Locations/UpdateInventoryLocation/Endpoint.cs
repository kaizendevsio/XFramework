using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Locations;

namespace Inventario.Api.Features.Locations.UpdateInventoryLocation;

public static class UpdateInventoryLocationEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/inventario/locations/update", Tags = ["Inventario"], Summary = "Update InventoryLocation metadata")]
    public static Task<Result<InventoryLocation>> Handle(UpdateInventoryLocationRequest request, WarehouseService service, CancellationToken ct) =>
        service.UpdateInventoryLocationAsync(request, ct);
}
