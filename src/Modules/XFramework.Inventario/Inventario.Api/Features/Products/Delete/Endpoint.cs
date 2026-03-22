using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;

namespace Inventario.Api.Features.Products.Delete;

public static class DeleteProductEndpoint
{
    [MapDelete("/api/products/{id:guid}", Tags = ["Products"],
        Summary = "Delete a product",
        Description = "Soft deletes a product from the inventory",
        ExcludeFromOpenApi = true)]
    public static async Task<Result> Handle(
        DeleteProductByIdRequest request,
        ProductService productService,
        CancellationToken ct)
    {
        var result = await productService.DeleteAsync(request.Id, ct);

        return result.IsSuccess
            ? Result.Success()
            : Result.Failure(result.Message!, result.StatusCode);
    }
}

public record DeleteProductByIdRequest(Guid Id);
