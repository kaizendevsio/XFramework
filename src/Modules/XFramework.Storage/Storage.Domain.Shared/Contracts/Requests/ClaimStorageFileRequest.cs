using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record ClaimStorageFileRequest : RequestBase,
    ICommand<QueryResponse<StorageFileResponse>>,
    IBoltRequest<ClaimStorageFileRequest, QueryResponse<StorageFileResponse>>
{
    public Guid StorageFileId { get; set; }
}
