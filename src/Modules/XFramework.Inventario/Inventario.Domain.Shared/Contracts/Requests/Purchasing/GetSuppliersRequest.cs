using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

using TRequest = GetSuppliersRequest;
using TResponse = QueryResponse<List<Supplier>>;

[MemoryPackable]
public partial record GetSuppliersRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public bool IncludeInactive { get; init; }
}
