namespace Communications.Domain.Shared.Contracts.Requests.Threads;

using TRequest = GetThreadMessagesRequest;
using TResponse = QueryResponse<GetThreadMessagesResponse>;

[MemoryPackable]
public partial record GetThreadMessagesRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ThreadId { get; set; }
    public Guid RequesterCredentialId { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; } = 20;
    public Guid? ParentMessageId { get; set; }
    public Guid[]? MessageIds { get; set; }
    // Server-side push projection is not proof that the recipient received the message.
    public bool SuppressDeliveryAcknowledgement { get; set; }
}
