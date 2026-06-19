using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;

using TRequest = GetReservationAllocationStatusReportRequest;
using TResponse = QueryResponse<List<ReservationAllocationStatusReportRow>>;

[MemoryPackable]
public partial record GetReservationAllocationStatusReportRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid? ProductId { get; init; }
    public Guid? LotId { get; init; }
    public ReservationAllocationStatus? Status { get; init; }
}
