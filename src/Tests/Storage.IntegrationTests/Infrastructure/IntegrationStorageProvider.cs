using System.Security.Cryptography;
using Storage.Api.Services.Providers;
using Storage.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.Contracts;

namespace Storage.IntegrationTests.Infrastructure;

public sealed class IntegrationStorageProviderFactory(IntegrationStorageObjectProvider provider) : IStorageProviderFactory
{
    public IStorageObjectProvider Resolve(StorageProviderKind providerKind) => provider;
}

public sealed class IntegrationStorageObjectProvider : IStorageObjectProvider
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, SortedDictionary<int, byte[]>> bytesByFileId = [];

    public StorageProviderKind Kind => StorageProviderKind.S3Compatible;
    public int DeleteObjectCount { get; private set; }
    public int DeleteObjectAttemptCount { get; private set; }
    public int AbortUploadCount { get; private set; }
    public bool FailNextDelete { get; set; }
    public bool FailNextAbort { get; set; }

    public void Reset()
    {
        lock (gate)
        {
            bytesByFileId.Clear();
            DeleteObjectCount = 0;
            DeleteObjectAttemptCount = 0;
            AbortUploadCount = 0;
            FailNextDelete = false;
            FailNextAbort = false;
        }
    }

    public Task EnsureBucketAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        CancellationToken ct) =>
        Task.CompletedTask;

    public Task<string?> StartUploadAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        CancellationToken ct) =>
        Task.FromResult<string?>("integration-upload");

    public Task<string> UploadPartAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        StorageUploadSession session,
        StorageUploadPart part,
        byte[] bytes,
        CancellationToken ct)
    {
        lock (gate)
        {
            if (!bytesByFileId.TryGetValue(file.Id, out var parts))
            {
                parts = [];
                bytesByFileId[file.Id] = parts;
            }

            parts[part.PartNumber] = bytes.ToArray();
        }

        return Task.FromResult($"integration-part-{part.PartNumber}");
    }

    public Task<string?> CompleteUploadAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        StorageUploadSession session,
        IReadOnlyList<StorageUploadPart> parts,
        CancellationToken ct) =>
        Task.FromResult<string?>("integration-etag");

    public Task<string> ComputeObjectSha256Async(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        CancellationToken ct)
    {
        byte[] bytes;
        lock (gate)
        {
            bytes = bytesByFileId.TryGetValue(file.Id, out var parts)
                ? parts.OrderBy(part => part.Key).SelectMany(part => part.Value).ToArray()
                : [];
        }

        return Task.FromResult(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    public Task AbortUploadAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        StorageUploadSession session,
        CancellationToken ct)
    {
        AbortUploadCount++;
        if (FailNextAbort)
        {
            FailNextAbort = false;
            throw new InvalidOperationException("Injected abort failure.");
        }

        return Task.CompletedTask;
    }

    public Task DeleteObjectAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        CancellationToken ct)
    {
        DeleteObjectAttemptCount++;
        if (FailNextDelete)
        {
            FailNextDelete = false;
            throw new InvalidOperationException("Injected delete failure.");
        }

        DeleteObjectCount++;
        return Task.CompletedTask;
    }

    public Task<StorageDownloadUrlResponse> CreateDownloadUrlAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        DateTime expiresAt,
        CancellationToken ct) =>
        Task.FromResult(new StorageDownloadUrlResponse
        {
            StorageFileId = file.Id,
            Url = "https://signed.storage.integration/download",
            ExpiresAt = expiresAt,
            IsPublic = false
        });
}
