using Community.Domain.Shared.Contracts.Responses;

namespace Community.Domain.Shared.Contracts.Requests;

using TRequest = SearchIdentitiesRequest;
using TResponse = QueryResponse<PaginatedResult<SearchIdentitiesResponse>>;

[MemoryPackable]
public partial record SearchIdentitiesRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public string? SearchTerm { get; set; }
    public Guid? TypeId { get; set; }
    public Guid? RequestingIdentityId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
