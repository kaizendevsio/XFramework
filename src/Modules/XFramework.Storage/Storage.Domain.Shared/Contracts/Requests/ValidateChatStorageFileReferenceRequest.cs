using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record ValidateChatStorageFileReferenceRequest : RequestBase,
    ICommand<QueryResponse<StorageFileValidationResponse>>,
    IBoltRequest<ValidateChatStorageFileReferenceRequest, QueryResponse<StorageFileValidationResponse>>
{
    public Guid ThreadId { get; set; }
    public Guid StorageFileId { get; set; }
}
