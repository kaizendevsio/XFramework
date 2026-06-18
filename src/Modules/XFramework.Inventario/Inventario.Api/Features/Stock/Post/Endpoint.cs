using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;

namespace Inventario.Api.Features.Stock.Post;

public static class PostStockMovementEndpoint
{
    [BoltHandler]
    [MapPost("/api/inventario/stock/post", Tags = ["Inventario Stock"],
        Summary = "Post an inventory stock movement",
        Description = "Creates an append-only stock movement and updates the matching stock balance.")]
    public static async Task<Result<StockPostingResponse>> Handle(
        PostStockMovementRequest request,
        StockPostingService stockPostingService,
        CancellationToken ct)
    {
        return await stockPostingService.PostAsync(request, ct);
    }
}
