namespace Communications.Domain.Shared.Contracts.Requests.Threads;

[MemoryPackable]
public partial record GetDeferredEncryptionRequest : RequestBase,
    IQuery<QueryResponse<DeferredEncryptionResponse>>,
    IBoltRequest<GetDeferredEncryptionRequest, QueryResponse<DeferredEncryptionResponse>>
{
    public int PageIndex { get; set; }
}
