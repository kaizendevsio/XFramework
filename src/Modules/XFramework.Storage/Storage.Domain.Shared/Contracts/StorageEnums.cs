namespace XFramework.Domain.Shared.Contracts;

public enum StorageFileStatus
{
    Pending = 0,
    Uploading = 1,
    Available = 2,
    Deleted = 3,
    Failed = 4,
    Quarantined = 5,
    Verifying = 6,
    VerificationInProgress = 7,
    Deleting = 8
}

public enum StorageFileVisibility
{
    Private = 0,
    Public = 1
}

public enum StorageProviderKind
{
    AzureBlob = 0,
    S3Compatible = 1
}

public enum StorageUploadSessionStatus
{
    Created = 0,
    Uploading = 1,
    Completed = 2,
    Aborted = 3,
    Failed = 4,
    Expired = 5,
    Completing = 6,
    Aborting = 7
}

public enum StorageBucketPurpose
{
    Private = 0,
    Public = 1
}

public enum StoragePublicDeliveryMode
{
    Disabled = 0,
    ProviderManaged = 1,
    PrivateOriginCdn = 2
}

public enum StorageUploadPartStatus
{
    Uploaded = 0,
    Uploading = 1,
    Failed = 2
}
