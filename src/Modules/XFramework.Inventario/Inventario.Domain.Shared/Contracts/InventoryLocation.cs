using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Attributes;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/locations",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "inventario-locations"
)]
public partial class InventoryLocation : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid WarehouseId { get; set; }
    [MemoryPackIgnore]
    public Warehouse? Warehouse { get; set; }
    [MemoryPackOrder(1)]
    public Guid? ParentLocationId { get; set; }
    [MemoryPackIgnore]
    public InventoryLocation? ParentLocation { get; set; }
    [MemoryPackIgnore]
    public List<InventoryLocation> ChildLocations { get; set; } = new();
    [MemoryPackOrder(2)]
    public string Code { get; set; } = string.Empty;
    [MemoryPackOrder(3)]
    public string Name { get; set; } = string.Empty;
    [MemoryPackOrder(4)]
    public string? Description { get; set; }
    [MemoryPackOrder(5)]
    public InventoryLocationType LocationType { get; set; } = InventoryLocationType.Bin;
    [MemoryPackOrder(6)]
    public bool IsPickable { get; set; } = true;
}
