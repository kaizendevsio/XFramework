using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record CompleteStorageUploadSessionRequest : RequestBase,
    ICommand<QueryResponse<StorageFileResponse>>,
    IBoltRequest<CompleteStorageUploadSessionRequest, QueryResponse<StorageFileResponse>>
{
    public Guid UploadSessionId { get; set; }
    public string? ExpectedSha256Hash { get; set; }
}
