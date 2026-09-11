using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetChatStorageDownloadUrlRequest : RequestBase,
    ICommand<QueryResponse<StorageDownloadUrlResponse>>,
    IBoltRequest<GetChatStorageDownloadUrlRequest, QueryResponse<StorageDownloadUrlResponse>>
{
    public Guid ThreadId { get; set; }
    public Guid StorageFileId { get; set; }
    public int? ExpirationMinutes { get; set; }
}
