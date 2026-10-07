using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;

[MemoryPackable]
public partial record GetInventoryReportSnapshotRequest : RequestBase,
    IQuery<QueryResponse<InventoryReportSnapshot>>,
    IBoltRequest<GetInventoryReportSnapshotRequest, QueryResponse<InventoryReportSnapshot>>
{
    public Guid? ProductId { get; init; }
    public Guid? WarehouseId { get; init; }
    public Guid? LocationId { get; init; }
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc { get; init; }
    public int DaysAhead { get; init; } = 30;
}
