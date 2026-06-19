using XFramework.Domain.Shared.Attributes;
using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/reorder-rules",
    RequireAuthorization = true,
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "inventario-reorder-rules"
)]
public partial class InventoryReorderRule : BaseModel
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
    public decimal MinimumQuantity { get; set; }
    [MemoryPackOrder(4)]
    public decimal? MaximumQuantity { get; set; }
    [MemoryPackOrder(5)]
    public decimal ReorderPoint { get; set; }
    [MemoryPackOrder(6)]
    public decimal ReorderQuantity { get; set; }
    [MemoryPackOrder(7)]
    public string? PreferredSupplier { get; set; }
    [MemoryPackOrder(8)]
    public bool IsActive { get; set; } = true;
}
