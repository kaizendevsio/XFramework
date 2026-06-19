using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

namespace Inventario.Api.Features.Purchasing.Suppliers;

public static class GetSuppliersEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/suppliers", Tags = ["Inventario Purchasing"],
        Summary = "List suppliers",
        Description = "Lists tenant-scoped inventory suppliers.")]
    public static async Task<Result<List<Supplier>>> Handle(
        [AsParameters] GetSuppliersRequest request,
        PurchasingService purchasingService,
        CancellationToken ct)
    {
        return await purchasingService.GetSuppliersAsync(request, ct);
    }
}
