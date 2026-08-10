namespace XFramework.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class StorageUploadPart : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid UploadSessionId { get; set; }

    [MemoryPackOrder(1)]
    public int PartNumber { get; set; }

    [MemoryPackOrder(2)]
    public long OffsetBytes { get; set; }

    [MemoryPackOrder(3)]
    public int SizeBytes { get; set; }

    [MemoryPackOrder(4)]
    public string Sha256Hash { get; set; } = string.Empty;

    [MemoryPackOrder(5)]
    public string? ProviderPartId { get; set; }

    [MemoryPackOrder(6)]
    public DateTime UploadedAt { get; set; }

    [MemoryPackOrder(7)]
    public virtual StorageUploadSession UploadSession { get; set; } = null!;

    [MemoryPackOrder(8)]
    public StorageUploadPartStatus Status { get; set; } = StorageUploadPartStatus.Uploaded;
}
