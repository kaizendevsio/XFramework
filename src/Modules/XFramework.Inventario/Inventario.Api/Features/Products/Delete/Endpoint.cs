using XFramework.Inventario.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;

namespace Inventario.Api.Features.Products.Delete;

/// <summary>
/// Delete Product endpoint (soft delete)
/// </summary>
public static class DeleteProductEndpoint
{
    public static void MapDeleteProduct(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/products/{id:guid}", Handle)
            .WithName("DeleteProduct")
            .WithTags("Products")
            .WithOpenApi(op =>
            {
                op.Summary = "Delete a product";
                op.Description = "Soft deletes a product from the inventory";
                return op;
            })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Handle(
        Guid id,
        ProductService productService,
        IOutputCacheStore cacheStore,
        CancellationToken ct)
    {
        var result = await productService.DeleteAsync(id, ct);

        if (!result.IsSuccess)
        {
            return result.StatusCode == 404
                ? TypedResults.NotFound()
                : TypedResults.Problem(
                    title: "Error deleting product",
                    detail: result.Message,
                    statusCode: result.StatusCode
                );
        }

        // Invalidate output cache for Products endpoints
        await cacheStore.EvictByTagAsync("Products", ct);

        return TypedResults.NoContent();
    }
}