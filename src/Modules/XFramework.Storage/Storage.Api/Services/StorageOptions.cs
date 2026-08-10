using XFramework.Domain.Shared.Contracts;

namespace Storage.Api.Services;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string DefaultProvider { get; set; } = StorageProviderKind.S3Compatible.ToString();
    public string ProviderProfileName { get; set; } = "default";
    public string BucketPrefix { get; set; } = "xframework-dev";
    public int DefaultChunkSizeBytes { get; set; } = 5 * 1024 * 1024;
    public int SessionTtlMinutes { get; set; } = 24 * 60;
    public int SignedUrlExpirationMinutes { get; set; } = 15;
    public int MaxSignedUrlExpirationMinutes { get; set; } = 60;
    public long MaxFileSizeBytes { get; set; } = 5L * 1024 * 1024 * 1024 * 1024;
    public int RetentionDays { get; set; } = 30;
    public int UnclaimedFileTtlMinutes { get; set; } = 24 * 60;
    public int MaintenancePollSeconds { get; set; } = 30;
    public int MaintenanceBatchSize { get; set; } = 100;
    public int MaintenanceLeaseSeconds { get; set; } = 300;
    public int ReadinessTimeoutSeconds { get; set; } = 5;
    public bool AutoCreateBuckets { get; set; } = true;
    public bool EnforceProviderLimits { get; set; } = true;
    public S3StorageOptions S3 { get; set; } = new();
    public AzureBlobStorageOptions AzureBlob { get; set; } = new();

    public StorageProviderKind ResolveDefaultProviderKind()
    {
        return Enum.TryParse<StorageProviderKind>(DefaultProvider, ignoreCase: true, out var providerKind)
            ? providerKind
            : StorageProviderKind.S3Compatible;
    }
}

public sealed class S3StorageOptions
{
    public string? Endpoint { get; set; }
    public string Region { get; set; } = "us-east-1";
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }
    public string? AccessKeyIdSecretName { get; set; }
    public string? SecretAccessKeySecretName { get; set; }
    public bool UsePathStyle { get; set; } = true;
    public string? PublicBaseUrl { get; set; }
    public string? CdnBaseUrl { get; set; }
    public StoragePublicDeliveryMode PublicDeliveryMode { get; set; } = StoragePublicDeliveryMode.ProviderManaged;
    public string? ReadinessBucketName { get; set; }
}

public sealed class AzureBlobStorageOptions
{
    public string? ConnectionString { get; set; }
    public string? ConnectionStringSecretName { get; set; }
    public string? PublicBaseUrl { get; set; }
    public string? CdnBaseUrl { get; set; }
    public StoragePublicDeliveryMode PublicDeliveryMode { get; set; } = StoragePublicDeliveryMode.PrivateOriginCdn;
    public string? ReadinessBucketName { get; set; }
}
