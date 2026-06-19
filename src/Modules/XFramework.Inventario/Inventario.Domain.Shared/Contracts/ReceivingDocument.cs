using XFramework.Domain.Shared.Attributes;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/receiving",
    RequireAuthorization = true,
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "inventario-receiving"
)]
public partial class ReceivingDocument : BaseModel
{
    [MemoryPackOrder(0)]
    public string ReceiptNumber { get; set; } = string.Empty;
    [MemoryPackOrder(1)]
    public Guid? PurchaseOrderId { get; set; }
    [MemoryPackIgnore]
    public PurchaseOrder? PurchaseOrder { get; set; }
    [MemoryPackOrder(2)]
    public Guid WarehouseId { get; set; }
    [MemoryPackIgnore]
    public Warehouse? Warehouse { get; set; }
    [MemoryPackOrder(3)]
    public Guid LocationId { get; set; }
    [MemoryPackIgnore]
    public InventoryLocation? Location { get; set; }
    [MemoryPackOrder(4)]
    public Guid? SupplierId { get; set; }
    [MemoryPackIgnore]
    public Supplier? Supplier { get; set; }
    [MemoryPackOrder(5)]
    public ReceivingDocumentStatus Status { get; set; } = ReceivingDocumentStatus.Posted;
    [MemoryPackOrder(6)]
    public DateTime ReceivedAt { get; set; }
    [MemoryPackOrder(7)]
    public string? ReferenceNumber { get; set; }
    [MemoryPackOrder(8)]
    public string? Notes { get; set; }
    [MemoryPackOrder(9)]
    public string? IdempotencyKey { get; set; }
    [MemoryPackOrder(10)]
    public string? RequestHash { get; set; }
    [MemoryPackIgnore]
    public List<ReceivingLine> Lines { get; set; } = [];
}
