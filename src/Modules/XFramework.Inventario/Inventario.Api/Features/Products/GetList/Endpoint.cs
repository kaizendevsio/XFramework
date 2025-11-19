using Inventario.Core.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Features.Products.GetList;

/// <summary>
/// Get Products List endpoint with pagination and filtering
/// </summary>
public static class GetProductsListEndpoint
{
    public static void MapGetProductsList(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products", Handle)
            .WithName("GetProductsList")
            .WithTags("Products")
            .WithOpenApi(op =>
            {
                op.Summary = "Get a paginated list of products";
                op.Description = "Retrieves products with optional filtering by search term, category, and availability";
                return op;
            })
            .CacheOutput("ProductsPolicy") // Apply caching policy from Phase 1.4
            .Produces<PaginatedProductResponse>()
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<PaginatedProductResponse>, ProblemHttpResult>> Handle(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? search,
        [FromQuery] Guid? categoryId,
        [FromQuery] bool? isAvailable,
        ProductService productService,
        CancellationToken ct)
    {
        // Set defaults and limits
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100); // Max 100 items per page

        var request = new GetProductsRequest
        {
            Page = page,
            PageSize = pageSize,
            Search = search,
            CategoryId = categoryId,
            IsAvailable = isAvailable
        };

        var result = await productService.GetListAsync(request, ct);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Error retrieving products",
                detail: result.Message,
                statusCode: result.StatusCode
            );
        }

        var paginatedList = result.Data!;
        var response = new PaginatedProductResponse
        {
            Items = paginatedList.Items.Select(ProductResponse.FromProduct).ToList(),
            Page = paginatedList.Page,
            PageSize = paginatedList.PageSize,
            TotalCount = paginatedList.TotalCount,
            TotalPages = paginatedList.TotalPages,
            HasPrevious = paginatedList.HasPrevious,
            HasNext = paginatedList.HasNext
        };

        return TypedResults.Ok(response);
    }
}