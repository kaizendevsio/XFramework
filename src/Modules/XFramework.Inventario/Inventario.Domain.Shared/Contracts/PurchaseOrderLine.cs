using XFramework.Domain.Shared.Attributes;
using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/purchase-order-lines",
    RequireAuthorization = true,
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "inventario-purchase-order-lines"
)]
public partial class PurchaseOrderLine : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid PurchaseOrderId { get; set; }
    [MemoryPackIgnore]
    public PurchaseOrder? PurchaseOrder { get; set; }
    [MemoryPackOrder(1)]
    public Guid ProductId { get; set; }
    [MemoryPackIgnore]
    public Product? Product { get; set; }
    [MemoryPackOrder(2)]
    public decimal OrderedQuantity { get; set; }
    [MemoryPackOrder(3)]
    public decimal ReceivedQuantity { get; set; }
    [MemoryPackOrder(4)]
    public decimal? UnitCost { get; set; }
    [MemoryPackOrder(5)]
    public string? UnitOfMeasure { get; set; }
    [MemoryPackOrder(6)]
    public string? Notes { get; set; }
}
