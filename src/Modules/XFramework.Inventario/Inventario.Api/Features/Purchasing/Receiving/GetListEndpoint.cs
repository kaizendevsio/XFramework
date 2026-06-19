using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

namespace Inventario.Api.Features.Purchasing.Receiving;

public static class GetReceivingDocumentsEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/receiving", Tags = ["Inventario Purchasing"],
        Summary = "List receiving documents",
        Description = "Lists posted receiving documents for the current tenant.")]
    public static async Task<Result<List<ReceivingDocument>>> Handle(
        [AsParameters] GetReceivingDocumentsRequest request,
        PurchasingService purchasingService,
        CancellationToken ct)
    {
        return await purchasingService.GetReceivingDocumentsAsync(request, ct);
    }
}
