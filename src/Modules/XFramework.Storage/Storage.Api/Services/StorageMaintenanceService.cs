using Microsoft.Extensions.Options;
using Storage.Api.Services.Providers;

namespace Storage.Api.Services;

public sealed record StorageMaintenanceBatchResult(
    int ExpiredUploadSessions,
    int DeletedUnclaimedFiles,
    int VerifiedFiles = 0);

public sealed class StorageMaintenanceService(
    AppDbContext db,
    IStorageProviderFactory providerFactory,
    IOptions<StorageOptions> options,
    TimeProvider timeProvider,
    ILogger<StorageMaintenanceService> logger)
{
    private readonly StorageOptions storageOptions = options.Value;

    public async Task<StorageMaintenanceBatchResult> RunBatchAsync(CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var batchSize = Math.Clamp(storageOptions.MaintenanceBatchSize, 1, 500);
        var leaseCutoff = now.AddSeconds(-Math.Max(30, storageOptions.MaintenanceLeaseSeconds));
        var verifiedFiles = await VerifyPendingFilesAsync(now, leaseCutoff, batchSize, ct);
        var sessions = await db.Set<StorageUploadSession>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(session => session.StorageFile)
            .Where(session => session.AbortedAt == null && session.ExpiresAt <= now)
            .Where(session => !db.Set<StorageUploadPart>().IgnoreQueryFilters().Any(part =>
                part.UploadSessionId == session.Id &&
                part.Status == StorageUploadPartStatus.Uploading &&
                (part.ModifiedAt == null || part.ModifiedAt > leaseCutoff)))
            .Where(session => session.Status == StorageUploadSessionStatus.Created ||
                              session.Status == StorageUploadSessionStatus.Uploading ||
                              session.Status == StorageUploadSessionStatus.Failed ||
                              session.Status == StorageUploadSessionStatus.Expired ||
                              session.Status == StorageUploadSessionStatus.Aborting &&
                              (session.ModifiedAt == null || session.ModifiedAt <= leaseCutoff))
            .OrderBy(session => session.ExpiresAt)
            .Take(batchSize)
            .ToListAsync(ct);
        var files = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(file => file.UnclaimedUntil != null && file.UnclaimedUntil <= now)
            .Where(file => file.ObjectDeletedAt == null)
            .Where(file => file.Status == StorageFileStatus.Available ||
                           file.Status == StorageFileStatus.Deleted ||
                           file.Status == StorageFileStatus.Deleting &&
                           (file.ModifiedAt == null || file.ModifiedAt <= leaseCutoff))
            .OrderBy(file => file.UnclaimedUntil)
            .Take(batchSize)
            .ToListAsync(ct);

        var providerIds = sessions.Select(session => session.StorageFile.ProviderProfileId)
            .Concat(files.Select(file => file.ProviderProfileId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var bucketIds = sessions.Select(session => session.StorageFile.TenantBucketId)
            .Concat(files.Select(file => file.TenantBucketId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var profiles = providerIds.Count == 0
            ? []
            : await db.Set<StorageProviderProfile>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(profile => providerIds.Contains(profile.Id))
                .ToDictionaryAsync(profile => profile.Id, ct);
        var buckets = bucketIds.Count == 0
            ? []
            : await db.Set<StorageTenantBucket>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(bucket => bucketIds.Contains(bucket.Id))
                .ToDictionaryAsync(bucket => bucket.Id, ct);

        var expiredSessions = 0;
        foreach (var session in sessions)
        {
            ct.ThrowIfCancellationRequested();
            var leaseToken = Guid.NewGuid();
            var claimed = await db.Set<StorageUploadSession>()
                .IgnoreQueryFilters()
                .Where(item => item.Id == session.Id && item.AbortedAt == null && item.ExpiresAt <= now)
                .Where(item => !db.Set<StorageUploadPart>().IgnoreQueryFilters().Any(part =>
                    part.UploadSessionId == item.Id &&
                    part.Status == StorageUploadPartStatus.Uploading &&
                    (part.ModifiedAt == null || part.ModifiedAt > leaseCutoff)))
                .Where(item => item.Status == StorageUploadSessionStatus.Created ||
                               item.Status == StorageUploadSessionStatus.Uploading ||
                               item.Status == StorageUploadSessionStatus.Failed ||
                               item.Status == StorageUploadSessionStatus.Expired ||
                               item.Status == StorageUploadSessionStatus.Aborting &&
                               (item.ModifiedAt == null || item.ModifiedAt <= leaseCutoff))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, StorageUploadSessionStatus.Aborting)
                    .SetProperty(item => item.ModifiedAt, now)
                    .SetProperty(item => item.ConcurrencyStamp, leaseToken), ct);
            if (claimed == 0)
                continue;

            if (!TryResolveProviderMetadata(session.StorageFile, profiles, buckets, out var profile, out var bucket))
            {
                logger.LogWarning(
                    "Expired storage upload session {UploadSessionId} is missing provider metadata.",
                    session.Id);
                await ReleaseSessionLeaseAsync(session.Id, leaseToken, now, ct);
                continue;
            }

            await db.Set<StorageFile>()
                .IgnoreQueryFilters()
                .Where(file => file.Id == session.StorageFileId && file.Status != StorageFileStatus.Available)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(file => file.Status, StorageFileStatus.Failed)
                    .SetProperty(file => file.ModifiedAt, now)
                    .SetProperty(file => file.ConcurrencyStamp, Guid.NewGuid()), ct);

            try
            {
                await providerFactory.Resolve(profile.Kind)
                    .AbortUploadAsync(profile, bucket, session.StorageFile, session, ct);
                await db.Set<StorageUploadSession>()
                    .IgnoreQueryFilters()
                    .Where(item => item.Id == session.Id &&
                                   item.Status == StorageUploadSessionStatus.Aborting &&
                                   item.ConcurrencyStamp == leaseToken)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Status, StorageUploadSessionStatus.Aborted)
                        .SetProperty(item => item.AbortedAt, now)
                        .SetProperty(item => item.ModifiedAt, now)
                        .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);
                expiredSessions++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Expired storage upload session {UploadSessionId} could not be aborted.", session.Id);
                await ReleaseSessionLeaseAsync(session.Id, leaseToken, now, ct);
            }
        }

        var deletedFiles = 0;
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var leaseToken = Guid.NewGuid();
            var claimed = await db.Set<StorageFile>()
                .IgnoreQueryFilters()
                .Where(item => item.Id == file.Id && item.ObjectDeletedAt == null)
                .Where(item => item.UnclaimedUntil != null && item.UnclaimedUntil <= now)
                .Where(item => item.Status == StorageFileStatus.Available ||
                               item.Status == StorageFileStatus.Deleted ||
                               item.Status == StorageFileStatus.Deleting &&
                               (item.ModifiedAt == null || item.ModifiedAt <= leaseCutoff))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, StorageFileStatus.Deleting)
                    .SetProperty(item => item.IsDeleted, true)
                    .SetProperty(item => item.DeletedAt, now)
                    .SetProperty(item => item.RetentionUntil, now)
                    .SetProperty(item => item.ModifiedAt, now)
                    .SetProperty(item => item.ConcurrencyStamp, leaseToken), ct);
            if (claimed == 0)
                continue;

            if (!TryResolveProviderMetadata(file, profiles, buckets, out var profile, out var bucket))
            {
                logger.LogWarning("Expired unclaimed storage file {StorageFileId} is missing provider metadata.", file.Id);
                await ReleaseFileDeletionLeaseAsync(file.Id, leaseToken, now, ct);
                continue;
            }

            try
            {
                await providerFactory.Resolve(profile.Kind).DeleteObjectAsync(profile, bucket, file, ct);
                await db.Set<StorageFile>()
                    .IgnoreQueryFilters()
                    .Where(item => item.Id == file.Id &&
                                   item.ObjectDeletedAt == null &&
                                   item.Status == StorageFileStatus.Deleting &&
                                   item.ConcurrencyStamp == leaseToken)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Status, StorageFileStatus.Deleted)
                        .SetProperty(item => item.ObjectDeletedAt, now)
                        .SetProperty(item => item.ModifiedAt, now)
                        .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);
                deletedFiles++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Expired unclaimed storage file {StorageFileId} could not be deleted.", file.Id);
                await ReleaseFileDeletionLeaseAsync(file.Id, leaseToken, now, ct);
            }
        }

        return new StorageMaintenanceBatchResult(expiredSessions, deletedFiles, verifiedFiles);
    }

    private async Task<int> VerifyPendingFilesAsync(
        DateTime now,
        DateTime leaseCutoff,
        int batchSize,
        CancellationToken ct)
    {
        var candidates = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(file => file.Status == StorageFileStatus.Verifying ||
                           file.Status == StorageFileStatus.VerificationInProgress &&
                           (file.ModifiedAt == null || file.ModifiedAt <= leaseCutoff))
            .OrderBy(file => file.UploadedAt)
            .Take(batchSize)
            .ToListAsync(ct);
        var verified = 0;

        foreach (var candidate in candidates)
        {
            var leaseToken = Guid.NewGuid();
            var claimed = await db.Set<StorageFile>()
                .IgnoreQueryFilters()
                .Where(file => file.Id == candidate.Id &&
                               (file.Status == StorageFileStatus.Verifying ||
                                file.Status == StorageFileStatus.VerificationInProgress &&
                                (file.ModifiedAt == null || file.ModifiedAt <= leaseCutoff)))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(file => file.Status, StorageFileStatus.VerificationInProgress)
                    .SetProperty(file => file.ModifiedAt, now)
                    .SetProperty(file => file.ConcurrencyStamp, leaseToken), ct);
            if (claimed == 0)
                continue;

            var profile = await db.Set<StorageProviderProfile>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == candidate.ProviderProfileId && item.IsEnabled && !item.IsDeleted, ct);
            var bucket = await db.Set<StorageTenantBucket>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == candidate.TenantBucketId && item.IsEnabled && !item.IsDeleted, ct);
            if (profile is null || bucket is null)
            {
                await ReleaseVerificationLeaseAsync(candidate.Id, leaseToken, now, ct);
                continue;
            }

            try
            {
                var provider = providerFactory.Resolve(profile.Kind);
                var metadata = await provider.GetObjectMetadataAsync(profile, bucket, candidate, ct);
                var actualHash = metadata is null
                    ? null
                    : await provider.ComputeObjectSha256Async(profile, bucket, candidate, ct);
                var expectedHash = candidate.Sha256Hash ?? candidate.Hash;
                var isValid = metadata is not null &&
                              metadata.ContentLength == candidate.ContentLengthBytes &&
                              actualHash is not null &&
                              (string.IsNullOrWhiteSpace(expectedHash) ||
                               string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase));

                if (isValid && candidate.Visibility == StorageFileVisibility.Public)
                    await provider.EnsurePublicAccessAsync(profile, bucket, candidate, ct);

                var completedAt = timeProvider.GetUtcNow().UtcDateTime;
                var publicUrl = isValid && candidate.Visibility == StorageFileVisibility.Public
                    ? StorageService.BuildPublicUrl(profile, bucket, candidate.ObjectKey!, preferCdn: false)
                    : null;
                var cdnUrl = isValid && candidate.Visibility == StorageFileVisibility.Public
                    ? StorageService.BuildPublicUrl(profile, bucket, candidate.ObjectKey!, preferCdn: true)
                    : null;
                if (profile.Kind == StorageProviderKind.AzureBlob
                        ? storageOptions.AzureBlob.PublicDeliveryMode == StoragePublicDeliveryMode.PrivateOriginCdn
                        : storageOptions.S3.PublicDeliveryMode == StoragePublicDeliveryMode.PrivateOriginCdn)
                {
                    publicUrl = null;
                }
                var etag = metadata?.ETag ?? candidate.ETag;
                DateTime? completedTimestamp = isValid ? completedAt : null;
                var unclaimedUntil = isValid && candidate.UnclaimedUntil is not null
                    ? completedAt.AddMinutes(Math.Max(1, storageOptions.UnclaimedFileTtlMinutes))
                    : candidate.UnclaimedUntil;

                var updated = await db.Set<StorageFile>()
                    .IgnoreQueryFilters()
                    .Where(file => file.Id == candidate.Id &&
                                   file.Status == StorageFileStatus.VerificationInProgress &&
                                   file.ConcurrencyStamp == leaseToken)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(file => file.Status, isValid ? StorageFileStatus.Available : StorageFileStatus.Failed)
                        .SetProperty(file => file.Sha256Hash, actualHash)
                        .SetProperty(file => file.Hash, actualHash)
                        .SetProperty(file => file.ETag, etag)
                        .SetProperty(file => file.PublicUrl, string.IsNullOrWhiteSpace(publicUrl) ? null : publicUrl)
                        .SetProperty(file => file.CdnBaseUrl, string.IsNullOrWhiteSpace(cdnUrl) ? null : cdnUrl)
                        .SetProperty(file => file.CompletedAt, completedTimestamp)
                        .SetProperty(file => file.UnclaimedUntil, unclaimedUntil)
                        .SetProperty(file => file.ModifiedAt, completedAt)
                        .SetProperty(file => file.ConcurrencyStamp, Guid.NewGuid()), ct);
                if (updated > 0 && isValid)
                    verified++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Storage file {StorageFileId} verification failed and will be retried.", candidate.Id);
                await ReleaseVerificationLeaseAsync(candidate.Id, leaseToken, now, ct);
            }
        }

        return verified;
    }

    private Task ReleaseSessionLeaseAsync(Guid sessionId, Guid leaseToken, DateTime now, CancellationToken ct) =>
        db.Set<StorageUploadSession>()
            .IgnoreQueryFilters()
            .Where(item => item.Id == sessionId && item.ConcurrencyStamp == leaseToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, StorageUploadSessionStatus.Expired)
                .SetProperty(item => item.ModifiedAt, now)
                .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);

    private Task ReleaseFileDeletionLeaseAsync(Guid fileId, Guid leaseToken, DateTime now, CancellationToken ct) =>
        db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .Where(item => item.Id == fileId && item.ConcurrencyStamp == leaseToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, StorageFileStatus.Deleted)
                .SetProperty(item => item.ModifiedAt, now)
                .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);

    private Task ReleaseVerificationLeaseAsync(Guid fileId, Guid leaseToken, DateTime now, CancellationToken ct) =>
        db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .Where(item => item.Id == fileId && item.ConcurrencyStamp == leaseToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, StorageFileStatus.Verifying)
                .SetProperty(item => item.ModifiedAt, now)
                .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);

    private static bool TryResolveProviderMetadata(
        StorageFile file,
        IReadOnlyDictionary<Guid, StorageProviderProfile> profiles,
        IReadOnlyDictionary<Guid, StorageTenantBucket> buckets,
        out StorageProviderProfile profile,
        out StorageTenantBucket bucket)
    {
        if (file.ProviderProfileId is { } providerId &&
            file.TenantBucketId is { } bucketId &&
            profiles.TryGetValue(providerId, out var resolvedProfile) &&
            buckets.TryGetValue(bucketId, out var resolvedBucket))
        {
            profile = resolvedProfile;
            bucket = resolvedBucket;
            return true;
        }

        profile = null!;
        bucket = null!;
        return false;
    }
}
