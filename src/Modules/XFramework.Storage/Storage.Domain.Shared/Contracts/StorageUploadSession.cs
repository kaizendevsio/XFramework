namespace XFramework.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class StorageUploadSession : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid StorageFileId { get; set; }

    [MemoryPackOrder(1)]
    public string UploadId { get; set; } = string.Empty;

    [MemoryPackOrder(2)]
    public string? ProviderUploadId { get; set; }

    [MemoryPackOrder(3)]
    public StorageUploadSessionStatus Status { get; set; } = StorageUploadSessionStatus.Created;

    [MemoryPackOrder(4)]
    public int ChunkSizeBytes { get; set; }

    [MemoryPackOrder(5)]
    public long TotalSizeBytes { get; set; }

    [MemoryPackOrder(6)]
    public int TotalParts { get; set; }

    [MemoryPackOrder(7)]
    public string? ExpectedSha256Hash { get; set; }

    [MemoryPackOrder(8)]
    public DateTime ExpiresAt { get; set; }

    [MemoryPackOrder(9)]
    public DateTime? CompletedAt { get; set; }

    [MemoryPackOrder(10)]
    public DateTime? AbortedAt { get; set; }

    [MemoryPackOrder(11)]
    public virtual StorageFile StorageFile { get; set; } = null!;

    [MemoryPackOrder(12)]
    public virtual ICollection<StorageUploadPart> Parts { get; set; } = new List<StorageUploadPart>();
}
