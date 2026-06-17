using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Inventario.Domain.Shared.Contracts;

public class StockBalance : BaseModel
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Guid LocationId { get; set; }
    public InventoryLocation? Location { get; set; }
    public decimal OnHandQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal AvailableQuantity { get; set; }
    public DateTime? LastMovementAt { get; set; }
}
