using XFramework.Domain.Shared.Attributes;
using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/receiving-lines",
    RequireAuthorization = true,
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "inventario-receiving-lines"
)]
public partial class ReceivingLine : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid ReceivingDocumentId { get; set; }
    [MemoryPackIgnore]
    public ReceivingDocument? ReceivingDocument { get; set; }
    [MemoryPackOrder(1)]
    public Guid? PurchaseOrderLineId { get; set; }
    [MemoryPackIgnore]
    public PurchaseOrderLine? PurchaseOrderLine { get; set; }
    [MemoryPackOrder(2)]
    public Guid ProductId { get; set; }
    [MemoryPackIgnore]
    public Product? Product { get; set; }
    [MemoryPackOrder(3)]
    public Guid? LotId { get; set; }
    [MemoryPackIgnore]
    public InventoryLot? Lot { get; set; }
    [MemoryPackOrder(4)]
    public Guid? StockBalanceId { get; set; }
    [MemoryPackIgnore]
    public StockBalance? StockBalance { get; set; }
    [MemoryPackOrder(5)]
    public Guid? InventoryMovementId { get; set; }
    [MemoryPackIgnore]
    public InventoryMovement? InventoryMovement { get; set; }
    [MemoryPackOrder(6)]
    public decimal Quantity { get; set; }
    [MemoryPackOrder(7)]
    public decimal? UnitCost { get; set; }
    [MemoryPackOrder(8)]
    public string? UnitOfMeasure { get; set; }
    [MemoryPackOrder(9)]
    public string? LotNumber { get; set; }
}
