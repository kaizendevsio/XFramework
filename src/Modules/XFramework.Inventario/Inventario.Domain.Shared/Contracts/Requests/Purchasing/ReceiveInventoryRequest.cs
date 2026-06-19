using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

using TRequest = ReceiveInventoryRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record ReceiveInventoryRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public string? ReceiptNumber { get; init; }
    public Guid? PurchaseOrderId { get; init; }
    public Guid WarehouseId { get; init; }
    public Guid LocationId { get; init; }
    public Guid? SupplierId { get; init; }
    public DateTime? ReceivedAt { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? Notes { get; init; }
    public string? IdempotencyKey { get; init; }
    public List<ReceivingLineRequest> Lines { get; init; } = [];
}
