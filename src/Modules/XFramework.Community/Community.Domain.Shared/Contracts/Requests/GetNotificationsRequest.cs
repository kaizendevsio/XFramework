using Community.Domain.Shared.Contracts.Responses;

namespace Community.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetNotificationsRequest : RequestBase,
    IQuery<QueryResponse<GetNotificationsResponse>>,
    IBoltRequest<GetNotificationsRequest, QueryResponse<GetNotificationsResponse>>
{
    public Guid IdentityId { get; set; }
    public bool? IsRead { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; } = 20;
}
