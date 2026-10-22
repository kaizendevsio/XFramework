using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;

[MemoryPackable]
public partial record GetInventoryProductSalesRequest : RequestBase,
    IQuery<QueryResponse<InventoryProductSalesPage>>, IBoltRequest<GetInventoryProductSalesRequest, QueryResponse<InventoryProductSalesPage>>
{
    public DateTime FromUtc { get; set; } = DateTime.UtcNow.Date;
    public DateTime ToUtcExclusive { get; set; } = DateTime.UtcNow.Date.AddDays(1);
    public int Skip { get; set; }
    public int Take { get; set; } = 20;
    public string? ProductFilter { get; set; }
    public string? ProductFilterOperator { get; set; }
}
