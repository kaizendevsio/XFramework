using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;

using TRequest = GetExpiredStockReportRequest;
using TResponse = QueryResponse<List<NearExpiryStockReportRow>>;

[MemoryPackable]
public partial record GetExpiredStockReportRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid? ProductId { get; init; }
}
