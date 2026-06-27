using XFramework.Domain.Shared.Contracts.Responses;

namespace Messaging.Domain.Shared.Contracts.Requests.Attachments;

using TRequest = GetMessageFilesRequest;
using TResponse = QueryResponse<PaginatedResult<MessageFileResponse>>;

[MemoryPackable]
public partial record GetMessageFilesRequest : RequestBase,
    IQuery<QueryResponse<PaginatedResult<MessageFileResponse>>>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
    public Guid RequesterCredentialId { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; } = 20;
}
