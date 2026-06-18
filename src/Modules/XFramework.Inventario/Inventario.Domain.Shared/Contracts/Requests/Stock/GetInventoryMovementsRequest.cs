using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;

using TRequest = GetInventoryMovementsRequest;
using TResponse = QueryResponse<List<InventoryMovement>>;

[MemoryPackable]
public partial record GetInventoryMovementsRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid? ProductId { get; init; }
}
