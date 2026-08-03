using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record EnsureStorageUploadMetadataRequest : RequestBase,
    ICommand<QueryResponse<StorageUploadMetadataResponse>>,
    IBoltRequest<EnsureStorageUploadMetadataRequest, QueryResponse<StorageUploadMetadataResponse>>
{
    public string ContentType { get; set; } = string.Empty;
    public string IdentifierGroupName { get; set; } = string.Empty;
    public string IdentifierName { get; set; } = string.Empty;
    public string? IdentifierDescription { get; set; }
}
