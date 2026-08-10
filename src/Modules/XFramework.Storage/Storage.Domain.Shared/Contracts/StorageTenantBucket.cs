namespace XFramework.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class StorageTenantBucket : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid ProviderProfileId { get; set; }

    [MemoryPackOrder(1)]
    public string BucketName { get; set; } = string.Empty;

    [MemoryPackOrder(2)]
    public string? PublicBaseUrl { get; set; }

    [MemoryPackOrder(3)]
    public string? CdnBaseUrl { get; set; }

    [MemoryPackOrder(4)]
    public DateTime? LastEnsuredAt { get; set; }

    [MemoryPackOrder(5)]
    public virtual StorageProviderProfile ProviderProfile { get; set; } = null!;

    [MemoryPackOrder(6)]
    public virtual ICollection<StorageFile> Files { get; set; } = new List<StorageFile>();

    [MemoryPackOrder(7)]
    public StorageBucketPurpose Purpose { get; set; } = StorageBucketPurpose.Private;
}
