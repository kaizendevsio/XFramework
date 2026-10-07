using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts;

public sealed class InventarioSetup : BaseModel
{
    public InventarioSetupMode Mode { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public int LowStockThreshold { get; set; } = 5;
    public string DefaultCurrency { get; set; } = "PHP";
    public Guid? CompletionRequestId { get; set; }
    public string? CompletionHash { get; set; }
}
