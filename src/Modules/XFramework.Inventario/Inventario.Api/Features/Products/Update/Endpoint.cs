using FluentValidation;
using XFramework.Inventario.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;

namespace Inventario.Api.Features.Products.Update;

/// <summary>
/// Update Product endpoint
/// </summary>
public static class UpdateProductEndpoint
{
    public static void MapUpdateProduct(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/products/{id:guid}", Handle)
            .WithName("UpdateProduct")
            .WithTags("Products")
            .WithOpenApi(op =>
            {
                op.Summary = "Update an existing product";
                op.Description = "Updates a product and invalidates the cache";
                return op;
            })
            .Produces<ProductResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<ProductResponse>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
        Guid id,
        UpdateProductRequest request,
        ProductService productService,
        IValidator<UpdateProductRequest> validator,
        IOutputCacheStore cacheStore,
        CancellationToken ct)
    {
        // Validate request
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );

            return TypedResults.ValidationProblem(errors);
        }

        // Call service
        var result = await productService.UpdateAsync(id, request, ct);

        if (!result.IsSuccess)
        {
            return result.StatusCode == 404
                ? TypedResults.NotFound()
                : TypedResults.Problem(
                    title: "Error updating product",
                    detail: result.Message,
                    statusCode: result.StatusCode
                );
        }

        // Invalidate output cache for this product and list endpoints
        await cacheStore.EvictByTagAsync("Products", ct);

        // Map to response
        var response = ProductResponse.FromProduct(result.Data!);

        return TypedResults.Ok(response);
    }
}