using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetStorageDownloadUrlRequest : RequestBase,
    IQuery<QueryResponse<StorageDownloadUrlResponse>>,
    IBoltRequest<GetStorageDownloadUrlRequest, QueryResponse<StorageDownloadUrlResponse>>
{
    public Guid StorageFileId { get; set; }
    public int? ExpirationMinutes { get; set; }
}
