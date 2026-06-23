using Microsoft.AspNetCore.Mvc;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;

namespace Inventario.Api.Features.Variations.GetTypes;

public static class GetProductVariationTypesEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/product-variation-types", Tags = ["Inventario Variations"],
        Summary = "Get product variation types",
        Description = "Returns tenant-wide and product-local variation type lookups for the authenticated tenant.")]
    public static Task<Result<List<ProductVariationType>>> Handle(
        GetProductVariationTypesRequest request,
        [FromServices] ProductVariationService variationService,
        CancellationToken ct) =>
        variationService.GetTypesAsync(request, ct);
}
