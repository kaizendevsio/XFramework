using XFramework.Inventario.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Features.Products.Get;

/// <summary>
/// Get Product by ID endpoint
/// </summary>
public static class GetProductEndpoint
{
    public static void MapGetProduct(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products/{id:guid}", Handle)
            .WithName("GetProduct")
            .WithTags("Products")
            .WithOpenApi(op =>
            {
                op.Summary = "Get a product by ID";
                op.Description = "Retrieves a single product by its unique identifier";
                return op;
            })
            .CacheOutput("ProductsPolicy") // Apply caching policy from Phase 1.4
            .Produces<ProductResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<ProductResponse>, NotFound, ProblemHttpResult>> Handle(
        Guid id,
        ProductService productService,
        CancellationToken ct)
    {
        var result = await productService.GetByIdAsync(id, ct);

        if (!result.IsSuccess)
        {
            return result.StatusCode == 404
                ? TypedResults.NotFound()
                : TypedResults.Problem(
                    title: "Error retrieving product",
                    detail: result.Message,
                    statusCode: result.StatusCode
                );
        }

        var response = ProductResponse.FromProduct(result.Data!);
        return TypedResults.Ok(response);
    }
}