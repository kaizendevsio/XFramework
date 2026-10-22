using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace Inventario.Api.Features.Transactions.ProductSales;

public static class GetInventoryProductSalesEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["inventario.transactions:view"],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapGet("/api/inventario/product-transactions/summary", Tags = ["Inventario Transactions"],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["inventario.transactions:view"],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    public static Task<Result<InventoryProductSalesPage>> Handle(GetInventoryProductSalesRequest request,
        InventoryProductSalesService service, CancellationToken ct) => service.GetAsync(request, ct);
}
