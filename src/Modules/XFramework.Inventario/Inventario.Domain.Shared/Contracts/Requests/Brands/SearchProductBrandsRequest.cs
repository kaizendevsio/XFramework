using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Brands;

[MemoryPackable]
public partial record SearchProductBrandsRequest : RequestBase,
    IQuery<QueryResponse<List<string>>>,
    IBoltRequest<SearchProductBrandsRequest, QueryResponse<List<string>>>
{
    public Guid CategoryId { get; init; }
    public string? Search { get; init; }
    public int Limit { get; init; } = 20;
}

