using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;

namespace Inventario.Api.Features.Stock.PostBasic;

public static class PostBasicProductStockEndpoint
{
    [BoltHandler(
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["inventario.movements:create"],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/inventario/stock/post/basic", Tags = ["Inventario Stock"],
        Summary = "Receive product stock into the Basic stock area",
        RequiredActorCapabilities = ["inventario.movements:create"],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    public static Task<Result<StockPostingResponse>> Handle(
        PostBasicProductStockRequest request,
        BasicProductStockService service,
        CancellationToken ct) => service.PostAsync(request, ct);
}
