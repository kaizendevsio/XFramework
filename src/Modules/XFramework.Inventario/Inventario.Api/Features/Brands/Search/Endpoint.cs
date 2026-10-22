using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Brands;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Brands;

namespace Inventario.Api.Features.Brands.Search;

public static class SearchProductBrandsEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapGet("/api/inventario/catalog/brands", Tags = ["Inventario Brands"], Summary = "Search category product brands",
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    public static Task<Result<List<string>>> Handle(SearchProductBrandsRequest request, ProductBrandService service, CancellationToken ct) =>
        service.SearchAsync(request, ct);
}

