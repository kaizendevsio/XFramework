using XFramework.Domain.Shared.Attributes;

namespace XFramework.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/storage-files",
    RequireAuthorization = true,
    AuthorizationFeature = StorageAuthorizationCapabilities.Feature,
    ReadCapability = StorageAuthorizationCapabilities.ViewKey,
    CacheDurationSeconds = 0
)]
public partial class StorageFile : BaseModel
{
    [MemoryPackOrder(0)]
    public string ContentPath { get; set; } = string.Empty;

    [MemoryPackOrder(1)]
    public Guid TypeId { get; set; }

    [MemoryPackOrder(2)]
    public Guid Identifier { get; set; }

    [MemoryPackOrder(3)]
    public decimal? FileSize { get; set; }

    [MemoryPackOrder(4)]
    public DateTime? ExpireAt { get; set; }

    [MemoryPackOrder(5)]
    public Guid StorageFileIdentifierId { get; set; }

    [MemoryPackOrder(6)]
    public string? Hash { get; set; }

    [MemoryPackOrder(7)]
    public string? Name { get; set; }

    [MemoryPackOrder(8)]
    public string? ContentType { get; set; }

    [MemoryPackOrder(9)]
    public string? BlobContainer { get; set; }

    [MemoryPackOrder(10)]
    public StorageFileStatus Status { get; set; } = StorageFileStatus.Pending;

    [MemoryPackOrder(11)]
    public StorageFileVisibility Visibility { get; set; } = StorageFileVisibility.Private;

    [MemoryPackOrder(12)]
    public Guid? ProviderProfileId { get; set; }

    [MemoryPackOrder(13)]
    public Guid? TenantBucketId { get; set; }

    [MemoryPackOrder(14)]
    public string? ProviderProfileName { get; set; }

    [MemoryPackOrder(15)]
    public string? BucketName { get; set; }

    [MemoryPackOrder(16)]
    public string? ObjectKey { get; set; }

    [MemoryPackOrder(17)]
    public long? ContentLengthBytes { get; set; }

    [MemoryPackOrder(18)]
    public string? Sha256Hash { get; set; }

    [MemoryPackOrder(19)]
    public string? ETag { get; set; }

    [MemoryPackOrder(20)]
    public DateTime? UploadStartedAt { get; set; }

    [MemoryPackOrder(21)]
    public DateTime? UploadedAt { get; set; }

    [MemoryPackOrder(22)]
    public DateTime? CompletedAt { get; set; }

    [MemoryPackOrder(23)]
    public DateTime? RetentionUntil { get; set; }

    [MemoryPackOrder(24)]
    public string? PublicUrl { get; set; }

    [MemoryPackOrder(25)]
    public string? CdnBaseUrl { get; set; }

    [MemoryPackOrder(26)]
    public DateTime? DownloadUrlExpiresAt { get; set; }

    [MemoryPackOrder(27)]
    public DateTime? ObjectDeletedAt { get; set; }

    [MemoryPackOrder(28)]
    public DateTime? UnclaimedUntil { get; set; }

    [MemoryPackOrder(40)]
    public virtual StorageFileType Type { get; set; } = null!;

    [MemoryPackOrder(41)]
    public virtual StorageFileIdentifier? StorageFileIdentifier { get; set; }

    [MemoryPackOrder(42)]
    public virtual StorageProviderProfile? ProviderProfile { get; set; }

    [MemoryPackOrder(43)]
    public virtual StorageTenantBucket? TenantBucket { get; set; }

    [MemoryPackOrder(44)]
    public virtual ICollection<StorageUploadSession> UploadSessions { get; set; } = new List<StorageUploadSession>();
}

public class CreateStorageFileRequest
{
    public string ContentPath { get; set; } = string.Empty;
    public Guid TypeId { get; set; }
    public Guid Identifier { get; set; }
    public decimal? FileSize { get; set; }
    public DateTime? ExpireAt { get; set; }
    public Guid StorageFileIdentifierId { get; set; }
    public string? Hash { get; set; }
    public string? Name { get; set; }
    public string? ContentType { get; set; }
    public string? BlobContainer { get; set; }
}

public class UpdateStorageFileRequest
{
    public string ContentPath { get; set; } = string.Empty;
    public Guid TypeId { get; set; }
    public Guid Identifier { get; set; }
    public decimal? FileSize { get; set; }
    public DateTime? ExpireAt { get; set; }
    public Guid StorageFileIdentifierId { get; set; }
    public string? Hash { get; set; }
    public string? Name { get; set; }
    public string? ContentType { get; set; }
    public string? BlobContainer { get; set; }
}

public class GetStorageFileListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? TypeId { get; set; }
    public Guid? Identifier { get; set; }
    public Guid? StorageFileIdentifierId { get; set; }
}
