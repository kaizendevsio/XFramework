namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

[MemoryPackable]
public partial record ReceivingLineRequest
{
    public Guid? PurchaseOrderLineId { get; init; }
    public Guid ProductId { get; init; }
    public decimal Quantity { get; init; }
    public decimal? UnitCost { get; init; }
    public string? UnitOfMeasure { get; init; }
    public Guid? LotId { get; init; }
    public string? LotNumber { get; init; }
    public string? SupplierReference { get; init; }
    public DateTime? ManufacturedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
}
