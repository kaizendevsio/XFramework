using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

using TRequest = GetPurchaseOrderRequest;
using TResponse = QueryResponse<PurchaseOrder>;

[MemoryPackable]
public partial record GetPurchaseOrderRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid Id { get; init; }
}
