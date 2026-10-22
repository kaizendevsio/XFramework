using Microsoft.AspNetCore.Mvc;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;

namespace Inventario.Api.Features.Variations.Update;

public static class UpdateProductVariationEndpoint
{
    [BoltHandler(
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPut("/api/inventario/product-variations", Tags = ["Inventario Variations"],
        Summary = "Update product variant",
        Description = "Updates a variant or option combination and its absolute price. Combination edits require the saved concurrency stamp.")]
    public static Task<Result<ProductVariation>> Handle(
        UpdateProductVariationRequest request,
        [FromServices] ProductVariationService variationService,
        CancellationToken ct) =>
        variationService.UpdateVariationAsync(request, ct);
}
