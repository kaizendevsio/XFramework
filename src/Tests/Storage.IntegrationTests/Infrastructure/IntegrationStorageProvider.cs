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
    private TaskCompletionSource uploadPartStarted = CreateSignal();
    private TaskCompletionSource deleteObjectStarted = CreateSignal();

    public StorageProviderKind Kind => StorageProviderKind.S3Compatible;
    public int DeleteObjectCount => Volatile.Read(ref deleteObjectCount);
    public int DeleteObjectAttemptCount => Volatile.Read(ref deleteObjectAttemptCount);
    public int AbortUploadCount => Volatile.Read(ref abortUploadCount);
    public int UploadPartCount => Volatile.Read(ref uploadPartCount);
    public int CompleteUploadCount => Volatile.Read(ref completeUploadCount);
    public bool FailNextDelete { get; set; }
    public bool FailNextAbort { get; set; }
    public bool FailReadiness { get; set; }
    public int EnsurePublicAccessCount => Volatile.Read(ref ensurePublicAccessCount);
    public TimeSpan UploadPartDelay { get; set; }
    public TimeSpan CompleteUploadDelay { get; set; }
    public TimeSpan DeleteObjectDelay { get; set; }
    public Task UploadPartStarted => uploadPartStarted.Task;
    public Task DeleteObjectStarted => deleteObjectStarted.Task;

    public void Reset()
    {
        lock (gate)
        {
            bytesByFileId.Clear();
            deleteObjectCount = 0;
            deleteObjectAttemptCount = 0;
            abortUploadCount = 0;
            uploadPartCount = 0;
            completeUploadCount = 0;
            FailNextDelete = false;
            FailNextAbort = false;
            FailReadiness = false;
            ensurePublicAccessCount = 0;
            UploadPartDelay = TimeSpan.Zero;
            CompleteUploadDelay = TimeSpan.Zero;
            DeleteObjectDelay = TimeSpan.Zero;
            uploadPartStarted = CreateSignal();
            deleteObjectStarted = CreateSignal();
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

    public async Task<string> UploadPartAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        StorageUploadSession session,
        StorageUploadPart part,
        byte[] bytes,
        CancellationToken ct)
    {
        Interlocked.Increment(ref uploadPartCount);
        uploadPartStarted.TrySetResult();
        if (UploadPartDelay > TimeSpan.Zero)
            await Task.Delay(UploadPartDelay, ct);

        lock (gate)
        {
            if (!bytesByFileId.TryGetValue(file.Id, out var parts))
            {
                parts = [];
                bytesByFileId[file.Id] = parts;
            }

            parts[part.PartNumber] = bytes.ToArray();
        }

        return $"integration-part-{part.PartNumber}";
    }

    public async Task<string?> CompleteUploadAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        StorageUploadSession session,
        IReadOnlyList<StorageUploadPart> parts,
        CancellationToken ct)
    {
        Interlocked.Increment(ref completeUploadCount);
        if (CompleteUploadDelay > TimeSpan.Zero)
            await Task.Delay(CompleteUploadDelay, ct);
        return "integration-etag";
    }

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

    public Task<StorageObjectMetadata?> GetObjectMetadataAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        CancellationToken ct)
    {
        lock (gate)
        {
            var length = bytesByFileId.TryGetValue(file.Id, out var parts)
                ? parts.Sum(part => (long)part.Value.Length)
                : 0L;
            return Task.FromResult<StorageObjectMetadata?>(new StorageObjectMetadata(length, "integration-etag"));
        }
    }

    public Task EnsurePublicAccessAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        CancellationToken ct)
    {
        Interlocked.Increment(ref ensurePublicAccessCount);
        return Task.CompletedTask;
    }

    public Task CheckReadinessAsync(CancellationToken ct) =>
        FailReadiness
            ? Task.FromException(new InvalidOperationException("Injected readiness failure."))
            : Task.CompletedTask;

    public Task AbortUploadAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        StorageUploadSession session,
        CancellationToken ct)
    {
        Interlocked.Increment(ref abortUploadCount);
        if (FailNextAbort)
        {
            FailNextAbort = false;
            throw new InvalidOperationException("Injected abort failure.");
        }

        return Task.CompletedTask;
    }

    public async Task DeleteObjectAsync(
        StorageProviderProfile profile,
        StorageTenantBucket bucket,
        StorageFile file,
        CancellationToken ct)
    {
        Interlocked.Increment(ref deleteObjectAttemptCount);
        deleteObjectStarted.TrySetResult();
        if (DeleteObjectDelay > TimeSpan.Zero)
            await Task.Delay(DeleteObjectDelay, ct);
        if (FailNextDelete)
        {
            FailNextDelete = false;
            throw new InvalidOperationException("Injected delete failure.");
        }

        Interlocked.Increment(ref deleteObjectCount);
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

    private int ensurePublicAccessCount;
    private int abortUploadCount;
    private int uploadPartCount;
    private int completeUploadCount;
    private int deleteObjectCount;
    private int deleteObjectAttemptCount;

    private static TaskCompletionSource CreateSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
