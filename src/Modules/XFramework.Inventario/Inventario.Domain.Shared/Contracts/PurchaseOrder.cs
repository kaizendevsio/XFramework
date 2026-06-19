using XFramework.Domain.Shared.Attributes;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/purchase-orders",
    RequireAuthorization = true,
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "inventario-purchase-orders"
)]
public partial class PurchaseOrder : BaseModel
{
    [MemoryPackOrder(0)]
    public string OrderNumber { get; set; } = string.Empty;
    [MemoryPackOrder(1)]
    public Guid? SupplierId { get; set; }
    [MemoryPackIgnore]
    public Supplier? Supplier { get; set; }
    [MemoryPackOrder(2)]
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    [MemoryPackOrder(3)]
    public DateTime OrderDate { get; set; }
    [MemoryPackOrder(4)]
    public DateTime? ExpectedDate { get; set; }
    [MemoryPackOrder(5)]
    public string? Notes { get; set; }
    [MemoryPackIgnore]
    public List<PurchaseOrderLine> Lines { get; set; } = [];
}
