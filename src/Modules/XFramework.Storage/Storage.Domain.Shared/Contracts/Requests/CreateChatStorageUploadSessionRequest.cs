using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record CreateChatStorageUploadSessionRequest : RequestBase,
    ICommand<QueryResponse<StorageUploadSessionResponse>>,
    IBoltRequest<CreateChatStorageUploadSessionRequest, QueryResponse<StorageUploadSessionResponse>>
{
    public Guid ThreadId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long TotalSizeBytes { get; set; }
    public int? ChunkSizeBytes { get; set; }
    public string? ExpectedSha256Hash { get; set; }
}
