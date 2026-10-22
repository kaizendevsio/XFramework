namespace POS.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetPosSalesReportRequest : RequestBase,
    IQuery<QueryResponse<PosSalesReportResponse>>,
    IBoltRequest<GetPosSalesReportRequest, QueryResponse<PosSalesReportResponse>>
{
    public DateTime FromUtc { get; set; } = DateTime.UtcNow.Date;
    public DateTime ToUtcExclusive { get; set; } = DateTime.UtcNow.Date.AddDays(1);
    public Guid? RegisterId { get; set; }
    public Guid? CurrencyId { get; set; }
    public PosSaleStatus? Status { get; set; }
    public PosPaymentMethod? PaymentMethod { get; set; }
    public int? HourUtc { get; set; }
    public int Page { get; set; } = 1;
    public int RefundPage { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
