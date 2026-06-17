using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;

namespace Inventario.Api.Features.Products.GetList;

public static class GetProductsListEndpoint
{
    [MapGet("/api/products", Tags = ["Products"],
        Summary = "Get a paginated list of products",
        Description = "Retrieves products with optional filtering by search term, category, and availability")]
    public static async Task<Result<PaginatedProductResponse>> Handle(
        GetProductsRequest request,
        ProductService productService,
        CancellationToken ct)
    {
        // Normalize pagination defaults
        var normalizedRequest = request with
        {
            Page = request.Page <= 0 ? 1 : request.Page,
            PageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, 100)
        };

        var result = await productService.GetListAsync(normalizedRequest, ct);

        if (!result.IsSuccess)
            return Result<PaginatedProductResponse>.Failure(result.Message!, result.StatusCode);

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

        return Result<PaginatedProductResponse>.Success(response);
    }
}
