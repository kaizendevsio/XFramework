using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts;

public class InventoryMovement : BaseModel
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Guid? LocationId { get; set; }
    public InventoryLocation? Location { get; set; }
    public Guid? StockBalanceId { get; set; }
    public StockBalance? StockBalance { get; set; }
    public InventoryMovementType MovementType { get; set; }
    public decimal QuantityDelta { get; set; }
    public decimal QuantityBefore { get; set; }
    public decimal QuantityAfter { get; set; }
    public DateTime MovementDate { get; set; }
    public string? UnitOfMeasure { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Reason { get; set; }
}
