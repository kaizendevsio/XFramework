using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;

namespace Inventario.Api.Features.Products.Create;

public static class CreateProductEndpoint
{
    [BoltHandler(
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/products", Tags = ["Products"],
        Summary = "Create a new product",
        Description = "Creates a new product in the inventory system")]
    public static async Task<Result<ProductResponse>> Handle(
        CreateProductRequest request,
        ProductService productService,
        CancellationToken ct)
    {
        var result = await productService.CreateAsync(request, ct);

        if (!result.IsSuccess)
            return Result<ProductResponse>.Failure(result.Message!, result.StatusCode);

        var response = ProductResponse.FromProduct(result.Data!);
        return Result<ProductResponse>.Success(response);
    }
}
