using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;

using TRequest = GetInventoryReorderRulesRequest;
using TResponse = QueryResponse<List<InventoryReorderRule>>;

[MemoryPackable]
public partial record GetInventoryReorderRulesRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid? ProductId { get; init; }
    public Guid? ProductVariationId { get; init; }
    public bool IncludeInactive { get; init; }
}
