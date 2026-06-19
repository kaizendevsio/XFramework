using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

using TRequest = CreatePurchaseOrderRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreatePurchaseOrderRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public string? OrderNumber { get; init; }
    public Guid? SupplierId { get; init; }
    public PurchaseOrderStatus Status { get; init; } = PurchaseOrderStatus.Open;
    public DateTime? OrderDate { get; init; }
    public DateTime? ExpectedDate { get; init; }
    public string? Notes { get; init; }
    public List<PurchaseOrderLineRequest> Lines { get; init; } = [];
}
