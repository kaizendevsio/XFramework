using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

namespace Inventario.Api.Features.Purchasing.PurchaseOrders;

public static class CreatePurchaseOrderEndpoint
{
    [BoltHandler]
    [MapPost("/api/inventario/purchase-orders", Tags = ["Inventario Purchasing"],
        Summary = "Create purchase order",
        Description = "Creates a purchase order with line items.")]
    public static async Task<Result<PurchaseOrder>> Handle(
        CreatePurchaseOrderRequest request,
        PurchasingService purchasingService,
        CancellationToken ct)
    {
        return await purchasingService.CreatePurchaseOrderAsync(request, ct);
    }
}
