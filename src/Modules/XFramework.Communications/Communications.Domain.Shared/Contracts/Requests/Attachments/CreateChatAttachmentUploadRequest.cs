using Storage.Domain.Shared.Contracts.Responses;

namespace Communications.Domain.Shared.Contracts.Requests.Attachments;

[MemoryPackable]
public partial record CreateChatAttachmentUploadRequest : RequestBase,
    ICommand<QueryResponse<StorageUploadSessionResponse>>,
    IBoltRequest<CreateChatAttachmentUploadRequest, QueryResponse<StorageUploadSessionResponse>>
{
    public Guid ThreadId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long TotalSizeBytes { get; set; }
    public int? ChunkSizeBytes { get; set; }
    public string? ExpectedSha256Hash { get; set; }
}
