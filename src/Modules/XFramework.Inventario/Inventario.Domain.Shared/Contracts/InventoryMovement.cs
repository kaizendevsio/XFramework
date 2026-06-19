using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Attributes;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/stock/movements",
    RequireAuthorization = true,
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "inventario-stock-movements"
)]
public partial class InventoryMovement : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid ProductId { get; set; }
    [MemoryPackIgnore]
    public Product? Product { get; set; }
    [MemoryPackOrder(1)]
    public Guid? WarehouseId { get; set; }
    [MemoryPackIgnore]
    public Warehouse? Warehouse { get; set; }
    [MemoryPackOrder(2)]
    public Guid? LocationId { get; set; }
    [MemoryPackIgnore]
    public InventoryLocation? Location { get; set; }
    [MemoryPackOrder(3)]
    public Guid? StockBalanceId { get; set; }
    [MemoryPackIgnore]
    public StockBalance? StockBalance { get; set; }
    [MemoryPackOrder(4)]
    public Guid? LotId { get; set; }
    [MemoryPackIgnore]
    public InventoryLot? Lot { get; set; }
    [MemoryPackOrder(5)]
    public InventoryMovementType MovementType { get; set; }
    [MemoryPackOrder(6)]
    public decimal QuantityDelta { get; set; }
    [MemoryPackOrder(7)]
    public decimal QuantityBefore { get; set; }
    [MemoryPackOrder(8)]
    public decimal QuantityAfter { get; set; }
    [MemoryPackOrder(9)]
    public DateTime MovementDate { get; set; }
    [MemoryPackOrder(10)]
    public string? UnitOfMeasure { get; set; }
    [MemoryPackOrder(11)]
    public string? ReferenceType { get; set; }
    [MemoryPackOrder(12)]
    public Guid? ReferenceId { get; set; }
    [MemoryPackOrder(13)]
    public string? Reason { get; set; }
    [MemoryPackOrder(14)]
    public string? IdempotencyKey { get; set; }
    [MemoryPackOrder(15)]
    public string? RequestHash { get; set; }
}
