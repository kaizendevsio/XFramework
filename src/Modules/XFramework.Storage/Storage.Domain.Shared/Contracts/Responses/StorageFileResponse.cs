using XFramework.Domain.Shared.Contracts;

namespace Storage.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record StorageFileResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public Guid TypeId { get; set; }
    public Guid Identifier { get; set; }
    public Guid StorageFileIdentifierId { get; set; }
    public StorageFileStatus Status { get; set; }
    public StorageFileVisibility Visibility { get; set; }
    public string? ProviderProfileName { get; set; }
    public string? BucketName { get; set; }
    public string? ObjectKey { get; set; }
    public long? ContentLengthBytes { get; set; }
    public string? Sha256Hash { get; set; }
    public string? ETag { get; set; }
    public string? PublicUrl { get; set; }
    public string? CdnBaseUrl { get; set; }
    public DateTime? UploadStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? RetentionUntil { get; set; }
    public DateTime? ObjectDeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
