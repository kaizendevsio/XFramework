using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Lots;

namespace Inventario.Api.Features.Lots.UpdateInventoryLot;

public static class UpdateInventoryLotEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/inventario/lots/update", Tags = ["Inventario"], Summary = "Update InventoryLot metadata")]
    public static Task<Result<InventoryLot>> Handle(UpdateInventoryLotRequest request, InventoryLotService service, CancellationToken ct) =>
        service.UpdateInventoryLotAsync(request, ct);
}
