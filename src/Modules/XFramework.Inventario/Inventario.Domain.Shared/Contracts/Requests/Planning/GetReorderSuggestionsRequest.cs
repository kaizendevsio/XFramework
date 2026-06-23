using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;

using TRequest = GetReorderSuggestionsRequest;
using TResponse = QueryResponse<List<ReorderSuggestionRow>>;

[MemoryPackable]
public partial record GetReorderSuggestionsRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid? ProductId { get; init; }
    public Guid? ProductVariationId { get; init; }
    public Guid? WarehouseId { get; init; }
    public Guid? LocationId { get; init; }
}
