using XFramework.Domain.Shared.Attributes;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/lots",
    RequireAuthorization = true,
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "inventario-lots"
)]
public partial class InventoryLot : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid ProductId { get; set; }
    [MemoryPackIgnore]
    public Product? Product { get; set; }
    [MemoryPackOrder(1)]
    public Guid? ProductVariationId { get; set; }
    [MemoryPackIgnore]
    public ProductVariation? ProductVariation { get; set; }
    [MemoryPackOrder(2)]
    public string? LotNumber { get; set; }
    [MemoryPackOrder(3)]
    public string? SupplierReference { get; set; }
    [MemoryPackOrder(4)]
    public string? SourceReferenceType { get; set; }
    [MemoryPackOrder(5)]
    public Guid? SourceReferenceId { get; set; }
    [MemoryPackOrder(6)]
    public DateTime ReceivedAt { get; set; }
    [MemoryPackOrder(7)]
    public DateTime? ManufacturedAt { get; set; }
    [MemoryPackOrder(8)]
    public DateTime? ExpiresAt { get; set; }
    [MemoryPackOrder(9)]
    public decimal? UnitCost { get; set; }
    [MemoryPackOrder(10)]
    public InventoryLotStatus Status { get; set; }
}
