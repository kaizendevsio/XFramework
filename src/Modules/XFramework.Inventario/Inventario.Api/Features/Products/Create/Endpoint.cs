using FluentValidation;
using XFramework.Inventario.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Features.Products.Create;

/// <summary>
/// Create Product endpoint
/// </summary>
public static class CreateProductEndpoint
{
    public static void MapCreateProduct(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/products", Handle)
            .WithName("CreateProduct")
            .WithTags("Products")
            .WithOpenApi(op =>
            {
                op.Summary = "Create a new product";
                op.Description = "Creates a new product in the inventory system";
                return op;
            })
            .Produces<ProductResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Created<ProductResponse>, ValidationProblem, ProblemHttpResult>> Handle(
        CreateProductRequest request,
        ProductService productService,
        IValidator<CreateProductRequest> validator,
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
        var result = await productService.CreateAsync(request, ct);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Error creating product",
                detail: result.Message,
                statusCode: result.StatusCode
            );
        }

        // Map to response
        var response = ProductResponse.FromProduct(result.Data!);

        return TypedResults.Created($"/api/products/{response.Id}", response);
    }
}