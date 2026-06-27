using Storage.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.Contracts;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetStorageFilesRequest : RequestBase,
    IQuery<QueryResponse<StorageFileListResponse>>,
    IBoltRequest<GetStorageFilesRequest, QueryResponse<StorageFileListResponse>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? TypeId { get; set; }
    public Guid? Identifier { get; set; }
    public StorageFileStatus? Status { get; set; }
    public StorageFileVisibility? Visibility { get; set; }
}
