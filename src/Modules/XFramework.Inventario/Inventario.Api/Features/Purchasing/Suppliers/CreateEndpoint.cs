using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

namespace Inventario.Api.Features.Purchasing.Suppliers;

public static class CreateSupplierEndpoint
{
    [BoltHandler]
    [MapPost("/api/inventario/suppliers", Tags = ["Inventario Purchasing"],
        Summary = "Create supplier",
        Description = "Creates a tenant-scoped inventory supplier.")]
    public static async Task<Result<Supplier>> Handle(
        CreateSupplierRequest request,
        PurchasingService purchasingService,
        CancellationToken ct)
    {
        return await purchasingService.CreateSupplierAsync(request, ct);
    }
}
