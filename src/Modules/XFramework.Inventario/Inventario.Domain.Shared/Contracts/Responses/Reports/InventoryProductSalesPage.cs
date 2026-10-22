namespace XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

[MemoryPackable]
public partial record InventoryProductSalesPage(int TotalCount, List<InventoryProductSalesRow> Rows);

[MemoryPackable]
public partial record InventoryProductSalesRow(Guid ProductId, Guid? ProductVariationId, string ProductName,
    string? VariantName, string? SKU, decimal Quantity, decimal RecordedRevenue);
