namespace Messaging.Domain.Shared.Contracts.Requests.Threads;

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
}
