using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

[MemoryPackable]
public partial record LowStockReportRow(
    Guid ProductId,
    string ProductName,
    Guid? ProductVariationId,
    string? ProductVariationName,
    string? ProductVariationTypeName,
    Guid? WarehouseId,
    string? WarehouseName,
    Guid? LocationId,
    string? LocationName,
    decimal AvailableQuantity,
    decimal ReorderPoint,
    decimal MinimumQuantity);

[MemoryPackable]
public partial record ReorderSuggestionRow(
    Guid ProductId,
    string ProductName,
    Guid? ProductVariationId,
    string? ProductVariationName,
    string? ProductVariationTypeName,
    Guid? WarehouseId,
    string? WarehouseName,
    Guid? LocationId,
    string? LocationName,
    decimal AvailableQuantity,
    decimal ReorderPoint,
    decimal SuggestedQuantity,
    string? PreferredSupplier);

[MemoryPackable]
public partial record NearExpiryStockReportRow(
    Guid LotId,
    string LotNumber,
    Guid ProductId,
    string ProductName,
    Guid? ProductVariationId,
    string? ProductVariationName,
    string? ProductVariationTypeName,
    Guid? WarehouseId,
    string? WarehouseName,
    Guid? LocationId,
    string? LocationName,
    decimal OnHandQuantity,
    decimal AvailableQuantity,
    DateTime? ExpiresAt,
    InventoryLotStatus LotStatus);

[MemoryPackable]
public partial record StockPositionReportRow(
    Guid StockBalanceId,
    Guid ProductId,
    string ProductName,
    Guid? ProductVariationId,
    string? ProductVariationName,
    string? ProductVariationTypeName,
    Guid WarehouseId,
    string WarehouseName,
    Guid LocationId,
    string LocationName,
    Guid? LotId,
    string? LotNumber,
    decimal OnHandQuantity,
    decimal ReservedQuantity,
    decimal AvailableQuantity);

[MemoryPackable]
public partial record MovementLedgerReportRow(
    Guid MovementId,
    Guid ProductId,
    string ProductName,
    Guid? ProductVariationId,
    string? ProductVariationName,
    string? ProductVariationTypeName,
    Guid? WarehouseId,
    string WarehouseName,
    Guid? LocationId,
    string LocationName,
    Guid? LotId,
    string? LotNumber,
    InventoryMovementType MovementType,
    decimal QuantityDelta,
    string? ReferenceType,
    Guid? ReferenceId,
    DateTime MovementDate);

[MemoryPackable]
public partial record ReservationAllocationStatusReportRow(
    Guid AllocationId,
    Guid ReservationId,
    Guid ProductId,
    string ProductName,
    Guid? ProductVariationId,
    string? ProductVariationName,
    string? ProductVariationTypeName,
    Guid? LotId,
    string? LotNumber,
    decimal Quantity,
    ReservationAllocationStatus Status,
    DateTime ReservedAt,
    DateTime? ReleasedAt,
    DateTime? FulfilledAt);
