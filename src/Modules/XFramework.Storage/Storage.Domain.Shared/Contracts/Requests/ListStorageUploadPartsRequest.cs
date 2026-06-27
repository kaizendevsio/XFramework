using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record ListStorageUploadPartsRequest : RequestBase,
    IQuery<QueryResponse<StorageUploadPartListResponse>>,
    IBoltRequest<ListStorageUploadPartsRequest, QueryResponse<StorageUploadPartListResponse>>
{
    public Guid UploadSessionId { get; set; }
}
