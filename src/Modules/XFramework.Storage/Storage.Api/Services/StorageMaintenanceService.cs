using Microsoft.Extensions.Options;
using Storage.Api.Services.Providers;

namespace Storage.Api.Services;

public sealed record StorageMaintenanceBatchResult(
    int ExpiredUploadSessions,
    int DeletedUnclaimedFiles);

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
        var sessions = await db.Set<StorageUploadSession>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(session => session.StorageFile)
            .Where(session => session.AbortedAt == null && session.ExpiresAt <= now)
            .Where(session => session.Status == StorageUploadSessionStatus.Created ||
                              session.Status == StorageUploadSessionStatus.Uploading ||
                              session.Status == StorageUploadSessionStatus.Failed ||
                              session.Status == StorageUploadSessionStatus.Expired)
            .OrderBy(session => session.ExpiresAt)
            .Take(batchSize)
            .ToListAsync(ct);
        var files = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(file => file.UnclaimedUntil != null && file.UnclaimedUntil <= now)
            .Where(file => file.ObjectDeletedAt == null)
            .Where(file => file.Status == StorageFileStatus.Available || file.Status == StorageFileStatus.Deleted)
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
            if (!TryResolveProviderMetadata(session.StorageFile, profiles, buckets, out var profile, out var bucket))
            {
                logger.LogWarning(
                    "Expired storage upload session {UploadSessionId} is missing provider metadata.",
                    session.Id);
                continue;
            }

            var claimed = await db.Set<StorageUploadSession>()
                .IgnoreQueryFilters()
                .Where(item => item.Id == session.Id && item.AbortedAt == null && item.ExpiresAt <= now)
                .Where(item => item.Status == StorageUploadSessionStatus.Created ||
                               item.Status == StorageUploadSessionStatus.Uploading ||
                               item.Status == StorageUploadSessionStatus.Failed ||
                               item.Status == StorageUploadSessionStatus.Expired)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, StorageUploadSessionStatus.Expired)
                    .SetProperty(item => item.ModifiedAt, now)
                    .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);
            if (claimed == 0)
                continue;

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
                    .Where(item => item.Id == session.Id && item.Status == StorageUploadSessionStatus.Expired)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.AbortedAt, now)
                        .SetProperty(item => item.ModifiedAt, now)
                        .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);
                expiredSessions++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Expired storage upload session {UploadSessionId} could not be aborted.", session.Id);
            }
        }

        var deletedFiles = 0;
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            if (!TryResolveProviderMetadata(file, profiles, buckets, out var profile, out var bucket))
            {
                logger.LogWarning("Expired unclaimed storage file {StorageFileId} is missing provider metadata.", file.Id);
                continue;
            }

            if (file.Status == StorageFileStatus.Available)
            {
                var claimed = await db.Set<StorageFile>()
                    .IgnoreQueryFilters()
                    .Where(item => item.Id == file.Id)
                    .Where(item => item.Status == StorageFileStatus.Available)
                    .Where(item => item.UnclaimedUntil != null && item.UnclaimedUntil <= now)
                    .Where(item => item.ObjectDeletedAt == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Status, StorageFileStatus.Deleted)
                        .SetProperty(item => item.IsDeleted, true)
                        .SetProperty(item => item.DeletedAt, now)
                        .SetProperty(item => item.RetentionUntil, now)
                        .SetProperty(item => item.ModifiedAt, now)
                        .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);
                if (claimed == 0)
                    continue;
            }

            try
            {
                await providerFactory.Resolve(profile.Kind).DeleteObjectAsync(profile, bucket, file, ct);
                await db.Set<StorageFile>()
                    .IgnoreQueryFilters()
                    .Where(item => item.Id == file.Id && item.ObjectDeletedAt == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.ObjectDeletedAt, now)
                        .SetProperty(item => item.ModifiedAt, now)
                        .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);
                deletedFiles++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Expired unclaimed storage file {StorageFileId} could not be deleted.", file.Id);
            }
        }

        return new StorageMaintenanceBatchResult(expiredSessions, deletedFiles);
    }

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
