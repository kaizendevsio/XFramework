using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

using TRequest = GetPurchaseOrdersRequest;
using TResponse = QueryResponse<List<PurchaseOrder>>;

[MemoryPackable]
public partial record GetPurchaseOrdersRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public PurchaseOrderStatus? Status { get; init; }
    public Guid? SupplierId { get; init; }
}
