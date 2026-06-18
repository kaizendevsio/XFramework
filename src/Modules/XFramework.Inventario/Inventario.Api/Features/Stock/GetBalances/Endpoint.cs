using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;

namespace Inventario.Api.Features.Stock.GetBalances;

public static class GetStockBalancesEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/stock/balances", Tags = ["Inventario Stock"],
        Summary = "Get stock balances",
        Description = "Gets stock balances for the authenticated tenant.")]
    public static async Task<Result<List<StockBalance>>> Handle(
        GetStockBalancesRequest request,
        StockPostingService stockPostingService,
        CancellationToken ct)
    {
        return await stockPostingService.GetBalancesAsync(request, ct);
    }
}
