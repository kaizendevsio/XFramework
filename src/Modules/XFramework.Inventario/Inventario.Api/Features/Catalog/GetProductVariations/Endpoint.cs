using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Products;

namespace Inventario.Api.Features.Catalog.GetProductVariations;

public static class GetProductVariationsEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/catalog/products/{productId:guid}/variations", Tags = ["Inventario Catalog"],
        Summary = "Get product variations",
        Description = "Returns enabled sellable variants for one product.")]
    public static Task<Result<List<SellableProductVariationItem>>> Handle(
        GetProductVariationsRequest request,
        ProductService productService,
        CancellationToken ct) =>
        productService.GetProductVariationsAsync(request, ct);
}
