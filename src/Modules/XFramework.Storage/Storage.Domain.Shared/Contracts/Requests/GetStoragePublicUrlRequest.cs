using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetStoragePublicUrlRequest : RequestBase,
    IQuery<QueryResponse<StoragePublicUrlResponse>>,
    IBoltRequest<GetStoragePublicUrlRequest, QueryResponse<StoragePublicUrlResponse>>
{
    public Guid StorageFileId { get; set; }
}
