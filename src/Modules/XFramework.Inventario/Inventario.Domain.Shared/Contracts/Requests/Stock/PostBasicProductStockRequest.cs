using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;

[MemoryPackable]
public partial record PostBasicProductStockRequest : RequestBase,
    IQuery<QueryResponse<StockPostingResponse>>,
    IBoltRequest<PostBasicProductStockRequest, QueryResponse<StockPostingResponse>>
{
    public Guid ProductId { get; init; }
    public Guid? ProductVariationId { get; init; }
    public decimal Quantity { get; init; }
    public string? Reason { get; init; }
    public string IdempotencyKey { get; init; } = "";
}
