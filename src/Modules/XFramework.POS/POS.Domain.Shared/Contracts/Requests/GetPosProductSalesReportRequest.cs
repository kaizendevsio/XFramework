namespace POS.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetPosProductSalesReportRequest : RequestBase,
    IQuery<QueryResponse<PosProductSalesReportResponse>>,
    IBoltRequest<GetPosProductSalesReportRequest, QueryResponse<PosProductSalesReportResponse>>
{
    public DateTime FromUtc { get; set; } = DateTime.UtcNow.Date;
    public DateTime ToUtcExclusive { get; set; } = DateTime.UtcNow.Date.AddDays(1);
    public int Skip { get; set; }
    public int Take { get; set; } = 20;
    public string? ProductFilter { get; set; }
    public string? ProductFilterOperator { get; set; }
}
