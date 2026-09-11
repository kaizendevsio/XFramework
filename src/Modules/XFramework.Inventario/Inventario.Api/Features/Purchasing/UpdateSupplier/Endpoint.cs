using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

namespace Inventario.Api.Features.Purchasing.UpdateSupplier;

public static class UpdateSupplierEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/inventario/suppliers/update", Tags = ["Inventario"], Summary = "Update Supplier metadata")]
    public static Task<Result<Supplier>> Handle(UpdateSupplierRequest request, PurchasingService service, CancellationToken ct) =>
        service.UpdateSupplierAsync(request, ct);
}
