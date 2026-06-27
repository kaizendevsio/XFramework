namespace XFramework.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class StorageProviderProfile : BaseModel
{
    [MemoryPackOrder(0)]
    public string Name { get; set; } = "default";

    [MemoryPackOrder(1)]
    public StorageProviderKind Kind { get; set; } = StorageProviderKind.S3Compatible;

    [MemoryPackOrder(2)]
    public string? Endpoint { get; set; }

    [MemoryPackOrder(3)]
    public string? Region { get; set; }

    [MemoryPackOrder(4)]
    public string? AccessKeyId { get; set; }

    [MemoryPackOrder(5)]
    public string? SecretAccessKey { get; set; }

    [MemoryPackOrder(6)]
    public string? ConnectionString { get; set; }

    [MemoryPackOrder(7)]
    public string BucketPrefix { get; set; } = "xframework-dev";

    [MemoryPackOrder(8)]
    public string? PublicBaseUrl { get; set; }

    [MemoryPackOrder(9)]
    public string? CdnBaseUrl { get; set; }

    [MemoryPackOrder(10)]
    public bool UsePathStyle { get; set; } = true;

    [MemoryPackOrder(11)]
    public bool AutoCreateBuckets { get; set; } = true;

    [MemoryPackOrder(12)]
    public bool IsDefault { get; set; } = true;

    [MemoryPackOrder(13)]
    public string? AccessKeyIdSecretName { get; set; }

    [MemoryPackOrder(14)]
    public string? SecretAccessKeySecretName { get; set; }

    [MemoryPackOrder(15)]
    public string? ConnectionStringSecretName { get; set; }

    [MemoryPackOrder(16)]
    public virtual ICollection<StorageTenantBucket> TenantBuckets { get; set; } = new List<StorageTenantBucket>();

    [MemoryPackOrder(17)]
    public virtual ICollection<StorageFile> Files { get; set; } = new List<StorageFile>();
}
