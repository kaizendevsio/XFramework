using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record ValidateStorageFileReferenceRequest : RequestBase,
    IQuery<QueryResponse<StorageFileValidationResponse>>,
    IBoltRequest<ValidateStorageFileReferenceRequest, QueryResponse<StorageFileValidationResponse>>
{
    public Guid StorageFileId { get; set; }
    public bool RequireAvailable { get; set; } = true;
    public bool AllowDeleted { get; set; }
}
