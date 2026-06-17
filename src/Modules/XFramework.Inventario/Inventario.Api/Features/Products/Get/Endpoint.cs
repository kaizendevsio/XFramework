using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;

namespace Inventario.Api.Features.Products.Get;

public static class GetProductEndpoint
{
    [MapGet("/api/products/{id:guid}", Tags = ["Products"],
        Summary = "Get a product by ID",
        Description = "Retrieves a single product by its unique identifier")]
    public static async Task<Result<ProductResponse>> Handle(
        GetProductByIdRequest request,
        ProductService productService,
        CancellationToken ct)
    {
        var result = await productService.GetByIdAsync(request.Id, ct);

        if (!result.IsSuccess)
            return Result<ProductResponse>.Failure(result.Message!, result.StatusCode);

        var response = ProductResponse.FromProduct(result.Data!);
        return Result<ProductResponse>.Success(response);
    }
}

public record GetProductByIdRequest(Guid Id);
