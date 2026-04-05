using Community.Domain.Shared.Contracts.Responses;

namespace Community.Domain.Shared.Contracts.Requests;

using TRequest = GetContentFilesRequest;
using TResponse = QueryResponse<List<ContentFileResponse>>;

[MemoryPackable]
public partial record GetContentFilesRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ContentId { get; set; }
}
