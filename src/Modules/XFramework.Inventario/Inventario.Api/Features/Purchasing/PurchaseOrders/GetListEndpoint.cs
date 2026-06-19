using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

namespace Inventario.Api.Features.Purchasing.PurchaseOrders;

public static class GetPurchaseOrdersEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/purchase-orders", Tags = ["Inventario Purchasing"],
        Summary = "List purchase orders",
        Description = "Lists purchase orders for the current tenant.")]
    public static async Task<Result<List<PurchaseOrder>>> Handle(
        [AsParameters] GetPurchaseOrdersRequest request,
        PurchasingService purchasingService,
        CancellationToken ct)
    {
        return await purchasingService.GetPurchaseOrdersAsync(request, ct);
    }
}
