using Community.Domain.Shared.Contracts;

namespace Community.Domain.Shared.Contracts.Requests;

using TRequest = GetCommunityConnectionListRequest;
using TResponse = QueryResponse<List<CommunityConnection>>;

[MemoryPackable]
public partial record GetCommunityConnectionListRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ConnectionTypeId { get; set; }
    public Guid CommunityIdentityId { get; set; }
    public int Limit { get; set; } = 20;
}
