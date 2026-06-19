using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

namespace Inventario.Api.Features.Purchasing.PurchaseOrders;

public static class SetPurchaseOrderStatusEndpoint
{
    [BoltHandler]
    [MapPost("/api/inventario/purchase-orders/status", Tags = ["Inventario Purchasing"],
        Summary = "Set purchase order status",
        Description = "Opens or cancels a purchase order. Receiving controls partially received and received statuses.")]
    public static async Task<Result<PurchaseOrder>> Handle(
        SetPurchaseOrderStatusRequest request,
        PurchasingService purchasingService,
        CancellationToken ct)
    {
        return await purchasingService.SetPurchaseOrderStatusAsync(request, ct);
    }
}
