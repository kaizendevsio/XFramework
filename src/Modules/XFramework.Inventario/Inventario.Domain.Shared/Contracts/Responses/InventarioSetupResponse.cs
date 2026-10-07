using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record InventarioSetupResponse
{
    public Guid TenantId { get; init; }
    public Guid? ConcurrencyStamp { get; init; }
    public InventarioSetupMode? Mode { get; init; }
    public DateTime? CompletedAt { get; init; }
    public bool HasExistingConfiguration { get; init; }
    public bool WarehousingEnabled { get; init; }
    public bool CanManage { get; init; }
    public int LowStockThreshold { get; init; } = 5;
    public string DefaultCurrency { get; init; } = "PHP";
    public Guid? WarehouseId { get; init; }
    public Guid? LocationId { get; init; }
    public List<Warehouse> Warehouses { get; init; } = [];
    public List<InventoryLocation> Locations { get; init; } = [];
    [MemoryPackIgnore]
    public bool ShouldPrompt => CompletedAt is null && !HasExistingConfiguration;
}
