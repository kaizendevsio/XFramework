namespace Communications.Domain.Shared.Contracts.Requests.Threads;

using TRequest = GetThreadListRequest;
using TResponse = QueryResponse<GetThreadListResponse>;

[MemoryPackable]
public partial record GetThreadListRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid CredentialId { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; } = 20;
}
