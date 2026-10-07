namespace XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

[MemoryPackable]
public partial record InventoryReportSnapshot(
    Guid TenantId,
    DateTime GeneratedAtUtc,
    decimal OnHand,
    decimal Reserved,
    decimal Available,
    decimal Inbound,
    decimal Outbound,
    int PositionCount,
    int MovementCount,
    bool ExpiryEnabled,
    List<StockPositionReportRow> Positions,
    List<MovementLedgerReportRow> Movements,
    List<NearExpiryStockReportRow> NearExpiry,
    List<NearExpiryStockReportRow> Expired,
    List<InventoryReportFilterOption> Products,
    List<InventoryReportFilterOption> Warehouses,
    List<InventoryReportFilterOption> Locations);
