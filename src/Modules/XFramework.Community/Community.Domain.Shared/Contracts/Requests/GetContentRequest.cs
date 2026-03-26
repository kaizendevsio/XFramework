using Community.Domain.Shared.Contracts.Responses;

namespace Community.Domain.Shared.Contracts.Requests;

using TRequest = GetContentRequest;
using TResponse = QueryResponse<GetContentResponse>;

[MemoryPackable]
public partial record GetContentRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid Id { get; set; }
}
