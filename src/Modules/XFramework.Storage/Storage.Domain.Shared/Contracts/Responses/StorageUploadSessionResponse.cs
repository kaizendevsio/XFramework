using XFramework.Domain.Shared.Contracts;

namespace Storage.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record StorageUploadSessionResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid StorageFileId { get; set; }
    public string UploadId { get; set; } = string.Empty;
    public StorageUploadSessionStatus Status { get; set; }
    public int ChunkSizeBytes { get; set; }
    public long TotalSizeBytes { get; set; }
    public int TotalParts { get; set; }
    public int UploadedParts { get; set; }
    public string? ExpectedSha256Hash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public StorageFileResponse? File { get; set; }
}
