using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record CompleteChatStorageUploadSessionRequest : RequestBase,
    ICommand<QueryResponse<StorageFileResponse>>,
    IBoltRequest<CompleteChatStorageUploadSessionRequest, QueryResponse<StorageFileResponse>>
{
    public Guid UploadSessionId { get; set; }
    public string? ExpectedSha256Hash { get; set; }
}
