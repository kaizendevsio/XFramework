using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

namespace Inventario.Api.Features.Purchasing.PurchaseOrders;

public static class GetPurchaseOrderEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/purchase-orders/{id:guid}", Tags = ["Inventario Purchasing"],
        Summary = "Get purchase order",
        Description = "Gets a purchase order with line items.")]
    public static async Task<Result<PurchaseOrder>> Handle(
        GetPurchaseOrderRequest request,
        PurchasingService purchasingService,
        CancellationToken ct)
    {
        return await purchasingService.GetPurchaseOrderAsync(request, ct);
    }
}
