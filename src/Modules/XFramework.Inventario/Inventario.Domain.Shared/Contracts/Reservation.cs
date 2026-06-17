using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts;

public class Reservation : BaseModel
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Guid? LocationId { get; set; }
    public InventoryLocation? Location { get; set; }
    public Guid? StockBalanceId { get; set; }
    public StockBalance? StockBalance { get; set; }
    public decimal Quantity { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTime ReservedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime? FulfilledAt { get; set; }
}
