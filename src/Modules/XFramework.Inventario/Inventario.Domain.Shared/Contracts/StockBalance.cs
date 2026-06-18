using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Attributes;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/stock/balances",
    RequireAuthorization = true,
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "inventario-stock-balances"
)]
public partial class StockBalance : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid ProductId { get; set; }
    [MemoryPackIgnore]
    public Product? Product { get; set; }
    [MemoryPackOrder(1)]
    public Guid WarehouseId { get; set; }
    [MemoryPackIgnore]
    public Warehouse? Warehouse { get; set; }
    [MemoryPackOrder(2)]
    public Guid LocationId { get; set; }
    [MemoryPackIgnore]
    public InventoryLocation? Location { get; set; }
    [MemoryPackOrder(3)]
    public decimal OnHandQuantity { get; set; }
    [MemoryPackOrder(4)]
    public decimal ReservedQuantity { get; set; }
    [MemoryPackOrder(5)]
    public decimal AvailableQuantity { get; set; }
    [MemoryPackOrder(6)]
    public DateTime? LastMovementAt { get; set; }
}
