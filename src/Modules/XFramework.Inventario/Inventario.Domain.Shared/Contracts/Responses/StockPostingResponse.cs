namespace XFramework.Inventario.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record StockPostingResponse(
    Guid StockBalanceId,
    Guid ProductId,
    Guid WarehouseId,
    Guid LocationId,
    decimal OnHandQuantity,
    decimal ReservedQuantity,
    decimal AvailableQuantity);
