using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

namespace Inventario.Api.Features.Purchasing.Receiving;

public static class ReceiveInventoryEndpoint
{
    [BoltHandler]
    [MapPost("/api/inventario/receiving", Tags = ["Inventario Purchasing"],
        Summary = "Receive inventory",
        Description = "Posts received stock into a warehouse/location, optionally creating/selecting lots and updating a purchase order.")]
    public static async Task<Result<ReceivingDocument>> Handle(
        ReceiveInventoryRequest request,
        PurchasingService purchasingService,
        CancellationToken ct)
    {
        return await purchasingService.ReceiveAsync(request, ct);
    }
}
