using Storage.Domain.Shared.Contracts.Requests;
using Storage.Integration.Drivers;

namespace IdentityServer.Api.Services;

public sealed class StorageCleanupOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<StorageCleanupOutboxDispatcher> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private const int MaxAttempts = 8;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval, timeProvider);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOneAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Storage cleanup outbox dispatch failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task DispatchOneAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var id = await db.Set<StorageCleanupOutboxMessage>()
            .IgnoreQueryFilters()
            .Where(message => message.IsEnabled && !message.IsDeleted)
            .Where(message => message.ProcessedAt == null && message.DeadLetteredAt == null)
            .Where(message => message.NextAttemptAt == null || message.NextAttemptAt <= now)
            .Where(message => message.LeaseExpiresAt == null || message.LeaseExpiresAt <= now)
            .OrderBy(message => message.CreatedAt)
            .Select(message => (Guid?)message.Id)
            .FirstOrDefaultAsync(ct);
        if (id is null)
            return;

        var leaseOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        var claimed = await db.Set<StorageCleanupOutboxMessage>()
            .IgnoreQueryFilters()
            .Where(message => message.Id == id)
            .Where(message => message.IsEnabled && !message.IsDeleted)
            .Where(message => message.ProcessedAt == null && message.DeadLetteredAt == null)
            .Where(message => message.LeaseExpiresAt == null || message.LeaseExpiresAt <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.LeaseOwner, leaseOwner)
                .SetProperty(message => message.LeaseExpiresAt, now.Add(LeaseDuration))
                .SetProperty(message => message.ModifiedAt, now), ct);
        if (claimed == 0)
            return;

        var message = await db.Set<StorageCleanupOutboxMessage>()
            .IgnoreQueryFilters()
            .AsTracking()
            .SingleAsync(item => item.Id == id && item.LeaseOwner == leaseOwner, ct);
        var wrapper = scope.ServiceProvider.GetRequiredService<IStorageServiceWrapper>();
        try
        {
            var result = await wrapper.DeleteStorageFile(new DeleteStorageFileRequest
            {
                StorageFileId = message.StorageFileId,
                Metadata = new RequestMetadata
                {
                    TenantId = message.TenantId,
                    RequestId = message.RequestId
                }
            }, ct);
            if (!result.IsSuccess && result.HttpStatusCode != HttpStatusCode.NotFound)
                throw new InvalidOperationException("Storage did not accept cleanup.");

            message.ProcessedAt = timeProvider.GetUtcNow().UtcDateTime;
            message.LastError = null;
            message.NextAttemptAt = null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            message.Attempts++;
            message.LastAttemptAt = timeProvider.GetUtcNow().UtcDateTime;
            message.LastError = "Storage cleanup failed.";
            if (message.Attempts >= MaxAttempts)
            {
                message.DeadLetteredAt = timeProvider.GetUtcNow().UtcDateTime;
                message.NextAttemptAt = null;
            }
            else
            {
                message.NextAttemptAt = timeProvider.GetUtcNow().UtcDateTime.Add(
                    TimeSpan.FromSeconds(Math.Pow(2, Math.Clamp(message.Attempts, 1, 6)) * 5));
            }

            logger.LogWarning(ex, "Storage cleanup outbox message {OutboxMessageId} failed.", message.Id);
        }
        finally
        {
            message.LeaseOwner = null;
            message.LeaseExpiresAt = null;
            message.ModifiedAt = timeProvider.GetUtcNow().UtcDateTime;
            message.ConcurrencyStamp = Guid.NewGuid();
        }

        await db.SaveChangesAsync(ct);
    }
}
