using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;

using TRequest = GetProductVariationTypesRequest;
using TResponse = QueryResponse<List<ProductVariationType>>;

[MemoryPackable]
public partial record GetProductVariationTypesRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid? ProductId { get; init; }
    public bool IncludeTenantWide { get; init; } = true;
    public bool IncludeProductLocal { get; init; } = true;
}
