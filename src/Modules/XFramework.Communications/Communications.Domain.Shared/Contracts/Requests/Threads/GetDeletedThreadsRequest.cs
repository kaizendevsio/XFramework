using Communications.Domain.Shared.Contracts.Responses;

namespace Communications.Domain.Shared.Contracts.Requests.Threads;

[MemoryPackable]
public partial record GetDeletedThreadsRequest : RequestBase, IQuery<QueryResponse<GetDeletedThreadsResponse>>,
    IBoltRequest<GetDeletedThreadsRequest, QueryResponse<GetDeletedThreadsResponse>>
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; } = 100;
}
