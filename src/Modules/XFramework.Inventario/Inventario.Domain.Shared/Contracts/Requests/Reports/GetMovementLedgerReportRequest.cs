using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;

using TRequest = GetMovementLedgerReportRequest;
using TResponse = QueryResponse<List<MovementLedgerReportRow>>;

[MemoryPackable]
public partial record GetMovementLedgerReportRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid? ProductId { get; init; }
    public Guid? ProductVariationId { get; init; }
    public Guid? WarehouseId { get; init; }
    public Guid? LocationId { get; init; }
    public Guid? LotId { get; init; }
    public string? ReferenceType { get; init; }
    public Guid? ReferenceId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}
