using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Warehouses;

using TRequest = GetWarehousesRequest;
using TResponse = QueryResponse<List<Warehouse>>;

[MemoryPackable]
public partial record GetWarehousesRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid? Id { get; init; }
}
