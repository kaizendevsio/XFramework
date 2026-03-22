using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;

namespace Inventario.Api.Features.Products.Update;

public static class UpdateProductEndpoint
{
    [MapPut("/api/products/{id:guid}", Tags = ["Products"],
        Summary = "Update an existing product",
        Description = "Updates a product and invalidates the cache",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<ProductResponse>> Handle(
        UpdateProductByIdRequest request,
        ProductService productService,
        CancellationToken ct)
    {
        var result = await productService.UpdateAsync(request.Id, request.Body, ct);

        if (!result.IsSuccess)
            return Result<ProductResponse>.Failure(result.Message!, result.StatusCode);

        var response = ProductResponse.FromProduct(result.Data!);
        return Result<ProductResponse>.Success(response);
    }
}

public record UpdateProductByIdRequest(Guid Id, UpdateProductRequest Body);
