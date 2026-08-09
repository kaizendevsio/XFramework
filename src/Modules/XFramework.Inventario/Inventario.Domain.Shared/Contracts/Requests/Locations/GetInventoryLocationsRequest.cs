using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Locations;

using TRequest = GetInventoryLocationsRequest;
using TResponse = QueryResponse<List<InventoryLocation>>;

[MemoryPackable]
public partial record GetInventoryLocationsRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid? WarehouseId { get; init; }
    public Guid? Id { get; init; }
}
