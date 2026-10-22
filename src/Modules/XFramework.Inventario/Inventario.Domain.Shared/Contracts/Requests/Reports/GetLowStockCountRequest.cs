using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;

[MemoryPackable]
public partial record GetLowStockCountRequest : RequestBase,
    IQuery<QueryResponse<int>>, IBoltRequest<GetLowStockCountRequest, QueryResponse<int>>
{
    public Guid? ProductId { get; init; }
    public Guid? ProductVariationId { get; init; }
    public Guid? WarehouseId { get; init; }
    public Guid? LocationId { get; init; }
}
