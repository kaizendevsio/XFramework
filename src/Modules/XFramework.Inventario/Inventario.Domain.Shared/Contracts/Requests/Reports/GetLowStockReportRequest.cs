using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;

using TRequest = GetLowStockReportRequest;
using TResponse = QueryResponse<List<LowStockReportRow>>;

[MemoryPackable]
public partial record GetLowStockReportRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid? ProductId { get; init; }
    public Guid? ProductVariationId { get; init; }
    public Guid? WarehouseId { get; init; }
    public Guid? LocationId { get; init; }
}
