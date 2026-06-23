using Microsoft.AspNetCore.Mvc;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;

namespace Inventario.Api.Features.Variations.Create;

public static class CreateProductVariationEndpoint
{
    [BoltHandler]
    [MapPost("/api/inventario/product-variations", Tags = ["Inventario Variations"],
        Summary = "Create product variant",
        Description = "Creates a sellable product variant with an absolute catalog price.")]
    public static Task<Result<ProductVariation>> Handle(
        CreateProductVariationRequest request,
        [FromServices] ProductVariationService variationService,
        CancellationToken ct) =>
        variationService.CreateVariationAsync(request, ct);
}
