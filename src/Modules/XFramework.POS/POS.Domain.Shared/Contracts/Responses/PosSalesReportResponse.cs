namespace POS.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record PosSalesReportResponse(
    Guid TenantId, DateTime GeneratedAtUtc, DateTime FromUtc, DateTime ToUtcExclusive,
    int TotalCount, int Page, int PageSize,
    List<PosReportCurrencySummary> Currencies,
    List<PosReportDay> Timeline,
    List<PosReportStatusCount> Statuses,
    List<PosReportSaleRow> Rows)
{
    public int RefundCount { get; init; }
    public int RefundPage { get; init; } = 1;
    public List<PosReportRefundRow> RefundRows { get; init; } = [];
    public List<PosReportCurrencyLabel> CurrencyLabels { get; init; } = [];
}

[MemoryPackable]
public partial record PosReportCurrencyLabel(Guid CurrencyId, string Label);

[MemoryPackable]
public partial record PosReportCurrencySummary(Guid CurrencyId, int CompletedCount,
    decimal Subtotal, decimal Discount, decimal Tax, decimal Total,
    decimal CashTotal, decimal WalletTotal)
{
    public int RefundCount { get; init; }
    public decimal RefundTotal { get; init; }
    [MemoryPackIgnore] public decimal NetTotal => Total - RefundTotal;
}

[MemoryPackable]
public partial record PosReportDay(Guid CurrencyId, DateTime DateUtc, int Count, decimal Total)
{
    public decimal RefundTotal { get; init; }
    [MemoryPackIgnore] public decimal NetTotal => Total - RefundTotal;
}

[MemoryPackable]
public partial record PosReportRefundRow(Guid Id, string ReturnNumber, Guid SaleId, Guid CurrencyId,
    PosPaymentMethod RefundMethod, decimal TotalRefundAmount, DateTime RefundedAtUtc);

[MemoryPackable]
public partial record PosReportStatusCount(PosSaleStatus Status, int Count);

[MemoryPackable]
public partial record PosReportSaleRow(Guid Id, string SaleNumber, Guid RegisterId, string RegisterName,
    Guid CurrencyId, PosSaleStatus Status, PosPaymentMethod PaymentMethod,
    decimal TotalAmount, DateTime? CreatedAt, DateTime? CompletedAt);
