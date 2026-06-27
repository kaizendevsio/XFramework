namespace Storage.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record StoragePublicUrlResponse
{
    public Guid StorageFileId { get; set; }
    public string? PublicUrl { get; set; }
    public string? CdnUrl { get; set; }
    public bool IsPublic { get; set; }
}
