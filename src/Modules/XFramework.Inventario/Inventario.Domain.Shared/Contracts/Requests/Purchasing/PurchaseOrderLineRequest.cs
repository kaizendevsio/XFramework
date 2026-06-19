namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

[MemoryPackable]
public partial record PurchaseOrderLineRequest
{
    public Guid ProductId { get; init; }
    public decimal OrderedQuantity { get; init; }
    public decimal? UnitCost { get; init; }
    public string? UnitOfMeasure { get; init; }
    public string? Notes { get; init; }
}
