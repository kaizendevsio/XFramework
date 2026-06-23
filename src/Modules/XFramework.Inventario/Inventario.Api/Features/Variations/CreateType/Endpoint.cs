using Microsoft.AspNetCore.Mvc;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;

namespace Inventario.Api.Features.Variations.CreateType;

public static class CreateProductVariationTypeEndpoint
{
    [BoltHandler]
    [MapPost("/api/inventario/product-variation-types", Tags = ["Inventario Variations"],
        Summary = "Create product variation type",
        Description = "Creates a tenant-wide or product-local reusable variation type.")]
    public static Task<Result<ProductVariationType>> Handle(
        CreateProductVariationTypeRequest request,
        [FromServices] ProductVariationService variationService,
        CancellationToken ct) =>
        variationService.CreateTypeAsync(request, ct);
}
