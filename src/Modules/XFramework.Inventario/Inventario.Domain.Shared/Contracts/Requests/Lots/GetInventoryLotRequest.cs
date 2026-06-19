using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Lots;

using TRequest = GetInventoryLotRequest;
using TResponse = QueryResponse<InventoryLot>;

[MemoryPackable]
public partial record GetInventoryLotRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid Id { get; init; }
}
