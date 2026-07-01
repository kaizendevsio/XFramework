using Inventario.Integration.Drivers;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;

namespace POS.Api.Services;

public sealed class PosCatalogService(IInventarioServiceWrapper inventario)
{
    public async Task<Result<List<PosCatalogItemResponse>>> SearchAsync(
        SearchPosCatalogRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out _))
            return Result<List<PosCatalogItemResponse>>.Failure("Tenant ID is required", 400);

        var response = await inventario.SearchSellableProducts(new SearchSellableProductsRequest
        {
            Search = request.Search,
            CategoryId = request.CategoryId,
            IsAvailable = request.IsAvailable,
            IncludeBaseProducts = request.IncludeBaseProducts,
            IncludeVariants = request.IncludeVariants,
            Page = request.Page,
            PageSize = request.PageSize,
            Metadata = request.Metadata
        });

        if (!response.IsSuccess)
            return Result<List<PosCatalogItemResponse>>.Failure(
                response.Message ?? "Inventario catalog search failed",
                (int)response.HttpStatusCode);

        var items = response.Response?
            .Select(item => new PosCatalogItemResponse
            {
                ProductId = item.ProductId,
                ProductVariationId = item.ProductVariationId,
                DisplayName = item.DisplayName,
                ProductName = item.ProductName,
                VariantName = item.VariantName,
                SKU = item.SKU,
                Brand = item.Brand,
                Image = item.Image,
                CategoryId = item.CategoryId,
                CategoryName = item.CategoryName,
                IsAvailable = item.IsAvailable,
                Price = item.Price
            })
            .ToList() ?? [];

        return Result<List<PosCatalogItemResponse>>.Success(items);
    }
}
