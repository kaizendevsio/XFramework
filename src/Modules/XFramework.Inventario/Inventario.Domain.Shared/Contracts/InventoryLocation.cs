using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts;

public class InventoryLocation : BaseModel
{
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Guid? ParentLocationId { get; set; }
    public InventoryLocation? ParentLocation { get; set; }
    public List<InventoryLocation> ChildLocations { get; set; } = new();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public InventoryLocationType LocationType { get; set; } = InventoryLocationType.Bin;
    public bool IsPickable { get; set; } = true;
}
