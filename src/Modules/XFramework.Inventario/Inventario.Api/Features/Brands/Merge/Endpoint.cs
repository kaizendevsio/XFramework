using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Brands;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Brands;

namespace Inventario.Api.Features.Brands.Merge;

public static class MergeProductBrandsEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/inventario/catalog/brands/merge", Tags = ["Inventario Brands"], Summary = "Merge category product brands",
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    public static Task<Result<ProductBrandMergePreview>> Handle(MergeProductBrandsRequest request, ProductBrandService service, CancellationToken ct) =>
        service.MergeAsync(request, ct);
}

