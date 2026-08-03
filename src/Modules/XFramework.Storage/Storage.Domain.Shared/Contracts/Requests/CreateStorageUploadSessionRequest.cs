using Storage.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.Contracts;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record CreateStorageUploadSessionRequest : RequestBase,
    ICommand<QueryResponse<StorageUploadSessionResponse>>,
    IBoltRequest<CreateStorageUploadSessionRequest, QueryResponse<StorageUploadSessionResponse>>
{
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public Guid TypeId { get; set; }
    public Guid Identifier { get; set; }
    public Guid StorageFileIdentifierId { get; set; }
    public long TotalSizeBytes { get; set; }
    public string? ExpectedSha256Hash { get; set; }
    public int? ChunkSizeBytes { get; set; }
    public StorageFileVisibility Visibility { get; set; } = StorageFileVisibility.Private;
    public string? ProviderProfileName { get; set; }
    public bool RequireClaim { get; set; }
}
