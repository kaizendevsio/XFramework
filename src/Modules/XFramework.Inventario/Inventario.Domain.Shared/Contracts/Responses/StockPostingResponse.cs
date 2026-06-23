namespace XFramework.Inventario.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record StockPostingResponse(
    Guid StockBalanceId,
    Guid ProductId,
    Guid? ProductVariationId,
    Guid WarehouseId,
    Guid LocationId,
    decimal OnHandQuantity,
    decimal ReservedQuantity,
    decimal AvailableQuantity)
{
    public Guid? LotId { get; init; }
    public string? IdempotencyKey { get; init; }
    public bool IsIdempotentReplay { get; init; }
}
