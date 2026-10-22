using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Brands;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Brands;

namespace Inventario.Api.Features.Brands.PreviewMerge;

public static class PreviewProductBrandMergeEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/inventario/catalog/brands/merge/preview", Tags = ["Inventario Brands"], Summary = "Preview category product brand merge",
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    public static Task<Result<ProductBrandMergePreview>> Handle(PreviewProductBrandMergeRequest request, ProductBrandService service, CancellationToken ct) =>
        service.PreviewAsync(request, ct);
}

