using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record UploadChatStorageFilePartRequest : RequestBase,
    ICommand<QueryResponse<StorageUploadPartResponse>>,
    IBoltRequest<UploadChatStorageFilePartRequest, QueryResponse<StorageUploadPartResponse>>
{
    public Guid UploadSessionId { get; set; }
    public int PartNumber { get; set; }
    public long OffsetBytes { get; set; }
    public string? PartSha256Hash { get; set; }
    public byte[] ChunkBytes { get; set; } = [];
}
