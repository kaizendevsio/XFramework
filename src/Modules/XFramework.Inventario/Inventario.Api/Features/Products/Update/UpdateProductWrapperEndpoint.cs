using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;

namespace Inventario.Api.Features.Products.Update;

public static class UpdateProductWrapperEndpoint
{
    [BoltHandler]
    [MapPut("/api/products", Tags = ["Products"],
        Summary = "Update an existing product",
        Description = "Updates catalog fields for a product and invalidates the cache")]
    public static async Task<Result<ProductResponse>> Handle(
        UpdateProductRequest request,
        ProductService productService,
        CancellationToken ct)
    {
        var result = await productService.UpdateAsync(request.ProductId, request, ct);

        if (!result.IsSuccess)
            return Result<ProductResponse>.Failure(result.Message!, result.StatusCode);

        var response = ProductResponse.FromProduct(result.Data!);
        return Result<ProductResponse>.Success(response);
    }
}
