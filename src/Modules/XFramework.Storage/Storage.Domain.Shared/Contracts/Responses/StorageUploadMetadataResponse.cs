namespace Storage.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record StorageUploadMetadataResponse
{
    public Guid TypeId { get; init; }
    public Guid StorageFileIdentifierId { get; init; }
}
