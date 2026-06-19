using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

using TRequest = SetPurchaseOrderStatusRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record SetPurchaseOrderStatusRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid PurchaseOrderId { get; init; }
    public PurchaseOrderStatus Status { get; init; }
}
