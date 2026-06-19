using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;

using TRequest = GetNearExpiryStockReportRequest;
using TResponse = QueryResponse<List<NearExpiryStockReportRow>>;

[MemoryPackable]
public partial record GetNearExpiryStockReportRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public int DaysAhead { get; init; } = 30;
    public Guid? ProductId { get; init; }
}
