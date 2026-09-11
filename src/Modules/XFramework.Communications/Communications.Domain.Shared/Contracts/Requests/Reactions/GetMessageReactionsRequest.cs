using XFramework.Domain.Shared.Contracts.Responses;

namespace Communications.Domain.Shared.Contracts.Requests.Reactions;

[MemoryPackable]
public partial record GetMessageReactionsRequest : RequestBase,
    IQuery<QueryResponse<PaginatedResult<MessageReactionResponse>>>,
    IBoltRequest<GetMessageReactionsRequest, QueryResponse<PaginatedResult<MessageReactionResponse>>>
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; } = 100;
}
