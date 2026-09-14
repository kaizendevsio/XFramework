using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record UploadOwnAvatarFileRequest : RequestBase,
    ICommand<QueryResponse<StorageFileResponse>>,
    IBoltRequest<UploadOwnAvatarFileRequest, QueryResponse<StorageFileResponse>>
{
    public byte[] Bytes { get; set; } = [];
}
