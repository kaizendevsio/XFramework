namespace POS.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record PosProductSalesReportResponse(
    Guid TenantId, DateTime FromUtc, DateTime ToUtcExclusive,
    int TotalCount, List<PosProductSalesRow> Rows, List<PosReportCurrencyLabel> CurrencyLabels);

[MemoryPackable]
// SalesAmount is quantity * unit price less line discounts; excludes receipt adjustments, tax and refunds.
public partial record PosProductSalesRow(
    Guid ProductId, Guid? ProductVariationId, Guid CurrencyId,
    string ProductName, string? VariantName, string? SKU,
    decimal Quantity, decimal SalesAmount);
