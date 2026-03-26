using Community.Domain.Shared.Contracts.Responses;

namespace Community.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetFeedRequest : RequestBase,
    IQuery<QueryResponse<GetFeedResponse>>,
    IBoltRequest<GetFeedRequest, QueryResponse<GetFeedResponse>>
{
    public Guid IdentityId { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; } = 20;
}
