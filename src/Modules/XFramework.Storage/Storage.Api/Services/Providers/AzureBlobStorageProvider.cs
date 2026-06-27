using System.Text;
using System.Security.Cryptography;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Api.Services.Providers;

public sealed class AzureBlobStorageProvider(
    IOptions<StorageOptions> options,
    IConfiguration configuration) : IStorageObjectProvider
{
    private readonly StorageOptions storageOptions = options.Value;

    public StorageProviderKind Kind => StorageProviderKind.AzureBlob;

    public async Task EnsureBucketAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        CancellationToken ct)
    {
        var container = CreateContainer(profile, bucket);
        if (profile.AutoCreateBuckets)
            await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
    }

    public Task<string?> StartUploadAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        CancellationToken ct) =>
        Task.FromResult<string?>(null);

    public async Task<string> UploadPartAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        StorageUploadSession session,
        StorageUploadPart part,
        byte[] bytes,
        CancellationToken ct)
    {
        var blockBlob = CreateBlockBlob(profile, bucket, file);
        var blockId = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{session.UploadId}:{part.PartNumber:D8}"));

        await using var stream = new MemoryStream(bytes, writable: false);
        await blockBlob.StageBlockAsync(blockId, stream, cancellationToken: ct);

        return blockId;
    }

    public async Task<string?> CompleteUploadAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        StorageUploadSession session,
        IReadOnlyList<StorageUploadPart> parts,
        CancellationToken ct)
    {
        var blockBlob = CreateBlockBlob(profile, bucket, file);
        var blockIds = parts
            .OrderBy(part => part.PartNumber)
            .Select(part => part.ProviderPartId!)
            .ToList();

        var response = await blockBlob.CommitBlockListAsync(
            blockIds,
            new CommitBlockListOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = file.ContentType
                }
            },
            ct);

        return response.Value.ETag.ToString();
    }

    public async Task<string> ComputeObjectSha256Async(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        CancellationToken ct)
    {
        var blob = CreateBlob(profile, bucket, file);
        var response = await blob.DownloadStreamingAsync(cancellationToken: ct);
        await using var stream = response.Value.Content;
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public Task AbortUploadAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        StorageUploadSession session,
        CancellationToken ct)
    {
        // Azure automatically expires uncommitted staged blocks.
        return Task.CompletedTask;
    }

    public async Task DeleteObjectAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        CancellationToken ct)
    {
        var blob = CreateBlob(profile, bucket, file);
        await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
    }

    public Task<StorageDownloadUrlResponse> CreateDownloadUrlAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        DateTime expiresAt,
        CancellationToken ct)
    {
        var blob = CreateBlob(profile, bucket, file);
        if (!blob.CanGenerateSasUri)
            throw new InvalidOperationException("Azure Blob storage profile cannot generate private SAS download URLs. Use an account-key connection string or configure a supported SAS credential.");

        var url = blob.GenerateSasUri(BlobSasPermissions.Read, expiresAt).ToString();

        return Task.FromResult(new StorageDownloadUrlResponse
        {
            StorageFileId = file.Id,
            Url = url,
            ExpiresAt = expiresAt,
            IsPublic = false
        });
    }

    private BlobContainerClient CreateContainer(StorageProviderProfile profile, StorageTenantBucket bucket)
    {
        var connectionString = ResolveSecret(profile.ConnectionString, profile.ConnectionStringSecretName)
            ?? ResolveSecret(storageOptions.AzureBlob.ConnectionString, storageOptions.AzureBlob.ConnectionStringSecretName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Azure Blob storage requires Storage:AzureBlob:ConnectionString.");

        var serviceClient = new BlobServiceClient(connectionString);
        return serviceClient.GetBlobContainerClient(bucket.BucketName);
    }

    private BlobClient CreateBlob(StorageProviderProfile profile, StorageTenantBucket bucket, StorageFile file)
    {
        if (string.IsNullOrWhiteSpace(file.ObjectKey))
            throw new InvalidOperationException("Storage file object key is missing.");

        return CreateContainer(profile, bucket).GetBlobClient(file.ObjectKey);
    }

    private BlockBlobClient CreateBlockBlob(StorageProviderProfile profile, StorageTenantBucket bucket, StorageFile file)
    {
        if (string.IsNullOrWhiteSpace(file.ObjectKey))
            throw new InvalidOperationException("Storage file object key is missing.");

        return CreateContainer(profile, bucket).GetBlockBlobClient(file.ObjectKey);
    }

    private string? ResolveSecret(string? value, string? secretName)
    {
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        if (string.IsNullOrWhiteSpace(secretName))
            return null;

        return configuration[secretName] ?? Environment.GetEnvironmentVariable(secretName);
    }
}
