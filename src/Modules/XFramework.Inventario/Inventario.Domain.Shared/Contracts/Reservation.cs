using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Attributes;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/reservations",
    RequireAuthorization = true,
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "inventario-reservations"
)]
public partial class Reservation : BaseModel
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
    public decimal Quantity { get; set; }
    [MemoryPackOrder(5)]
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
    [MemoryPackOrder(6)]
    public string? ReferenceType { get; set; }
    [MemoryPackOrder(7)]
    public Guid? ReferenceId { get; set; }
    [MemoryPackOrder(8)]
    public DateTime ReservedAt { get; set; }
    [MemoryPackOrder(9)]
    public DateTime? ExpiresAt { get; set; }
    [MemoryPackOrder(10)]
    public DateTime? ReleasedAt { get; set; }
    [MemoryPackOrder(11)]
    public DateTime? FulfilledAt { get; set; }
}
