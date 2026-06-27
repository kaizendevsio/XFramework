namespace Storage.Api.Services.Providers;

public interface IStorageProviderFactory
{
    IStorageObjectProvider Resolve(StorageProviderKind providerKind);
}

public sealed class StorageProviderFactory(
    AzureBlobStorageProvider azureBlobStorageProvider,
    S3CompatibleStorageProvider s3CompatibleStorageProvider) : IStorageProviderFactory
{
    public IStorageObjectProvider Resolve(StorageProviderKind providerKind) =>
        providerKind switch
        {
            StorageProviderKind.AzureBlob => azureBlobStorageProvider,
            StorageProviderKind.S3Compatible => s3CompatibleStorageProvider,
            _ => throw new NotSupportedException($"Storage provider '{providerKind}' is not supported")
        };
}
