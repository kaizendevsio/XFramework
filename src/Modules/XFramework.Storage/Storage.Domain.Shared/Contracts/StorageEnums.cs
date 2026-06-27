namespace XFramework.Domain.Shared.Contracts;

public enum StorageFileStatus
{
    Pending = 0,
    Uploading = 1,
    Available = 2,
    Deleted = 3,
    Failed = 4,
    Quarantined = 5
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
    Expired = 5
}
