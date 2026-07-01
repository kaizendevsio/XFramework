using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Products;

namespace Inventario.Api.Features.Catalog.GetSellableProduct;

public static class GetSellableProductEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/catalog/sellable-products/{productId:guid}", Tags = ["Inventario Catalog"],
        Summary = "Get sellable product",
        Description = "Returns POS-friendly product catalog details and enabled variants.")]
    public static Task<Result<SellableProductDetail>> Handle(
        GetSellableProductRequest request,
        ProductService productService,
        CancellationToken ct) =>
        productService.GetSellableProductAsync(request, ct);
}
