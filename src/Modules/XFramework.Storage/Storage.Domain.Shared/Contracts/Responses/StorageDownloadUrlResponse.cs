namespace Storage.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record StorageDownloadUrlResponse
{
    public Guid StorageFileId { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsPublic { get; set; }
}
