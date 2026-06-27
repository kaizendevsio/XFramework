using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record RestoreStorageFileRequest : RequestBase,
    ICommand<QueryResponse<StorageFileResponse>>,
    IBoltRequest<RestoreStorageFileRequest, QueryResponse<StorageFileResponse>>
{
    public Guid StorageFileId { get; set; }
}
