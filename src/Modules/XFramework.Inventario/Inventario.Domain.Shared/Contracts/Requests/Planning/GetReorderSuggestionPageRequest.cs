using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;

[MemoryPackable]
public partial record GetReorderSuggestionPageRequest : RequestBase,
    IQuery<QueryResponse<ReorderSuggestionPage>>, IBoltRequest<GetReorderSuggestionPageRequest, QueryResponse<ReorderSuggestionPage>>
{
    public Guid? ProductId { get; init; }
    public Guid? ProductVariationId { get; init; }
    public Guid? WarehouseId { get; init; }
    public Guid? LocationId { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; } = 100;
    public string? SupplierFilter { get; init; }
    public string? SupplierFilterOperator { get; init; }
}
