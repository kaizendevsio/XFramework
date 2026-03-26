using Messaging.Domain.Shared.Contracts.Responses;

namespace Messaging.Domain.Shared.Contracts.Requests.Attachments;

using TRequest = GetMessageFilesRequest;
using TResponse = QueryResponse<List<MessageFileResponse>>;

[MemoryPackable]
public partial record GetMessageFilesRequest : RequestBase,
    IQuery<QueryResponse<List<MessageFileResponse>>>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
    public Guid RequesterCredentialId { get; set; }
}
