using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Products;

namespace Inventario.Api.Features.Catalog.SearchSellableProducts;

public static class SearchSellableProductsEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/catalog/sellable-products", Tags = ["Inventario Catalog"],
        Summary = "Search sellable products",
        Description = "Returns POS-friendly product and variant catalog rows for line selection.")]
    public static Task<Result<List<SellableProductCatalogItem>>> Handle(
        SearchSellableProductsRequest request,
        ProductService productService,
        CancellationToken ct) =>
        productService.SearchSellableProductsAsync(request, ct);
}
