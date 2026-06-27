using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetStorageFileRequest : RequestBase,
    IQuery<QueryResponse<StorageFileResponse>>,
    IBoltRequest<GetStorageFileRequest, QueryResponse<StorageFileResponse>>
{
    public Guid StorageFileId { get; set; }
}
