namespace Messaging.Domain.Shared.Contracts.Requests.Threads;

using TRequest = GetThreadRequest;
using TResponse = QueryResponse<GetThreadResponse>;

[MemoryPackable]
public partial record GetThreadRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid Id { get; set; }
}
