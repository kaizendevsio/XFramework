using Storage.Domain.Shared.Contracts.Requests;
using Storage.Integration.Drivers;

namespace IdentityServer.Api.Services;

public sealed class StorageClaimOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<StorageClaimOutboxDispatcher> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

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
                logger.LogError(ex, "Storage claim outbox dispatch failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task DispatchOneAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var id = await db.Set<StorageClaimOutboxMessage>()
            .IgnoreQueryFilters()
            .Where(message => !message.IsDeleted && message.IsEnabled)
            .Where(message => message.ProcessedAt == null && message.DeadLetteredAt == null)
            .Where(message => message.NextAttemptAt == null || message.NextAttemptAt <= now)
            .Where(message => message.LeaseExpiresAt == null || message.LeaseExpiresAt <= now)
            .OrderBy(message => message.CreatedAt)
            .Select(message => (Guid?)message.Id)
            .FirstOrDefaultAsync(ct);
        if (id is null)
            return;

        var leaseOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        var claimed = await db.Set<StorageClaimOutboxMessage>()
            .IgnoreQueryFilters()
            .Where(message => message.Id == id)
            .Where(message => message.ProcessedAt == null && message.DeadLetteredAt == null)
            .Where(message => message.NextAttemptAt == null || message.NextAttemptAt <= now)
            .Where(message => message.LeaseExpiresAt == null || message.LeaseExpiresAt <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.LeaseOwner, leaseOwner)
                .SetProperty(message => message.LeaseExpiresAt, now.Add(LeaseDuration))
                .SetProperty(message => message.Attempts, message => message.Attempts + 1)
                .SetProperty(message => message.LastAttemptAt, now)
                .SetProperty(message => message.ModifiedAt, now), ct);
        if (claimed == 0)
            return;

        var message = await db.Set<StorageClaimOutboxMessage>()
            .IgnoreQueryFilters()
            .AsTracking()
            .SingleAsync(item => item.Id == id && item.LeaseOwner == leaseOwner, ct);
        try
        {
            var wrapper = scope.ServiceProvider.GetRequiredService<IStorageServiceWrapper>();
            var result = await wrapper.ClaimStorageFile(new ClaimStorageFileRequest
            {
                StorageFileId = message.StorageFileId,
                Metadata = new RequestMetadata
                {
                    TenantId = message.TenantId,
                    RequestId = message.RequestId
                }
            }, ct);

            if (result.IsSuccess)
            {
                message.ProcessedAt = timeProvider.GetUtcNow().UtcDateTime;
                message.LastError = null;
                message.NextAttemptAt = null;
            }
            else if (result.HttpStatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict)
            {
                message.DeadLetteredAt = timeProvider.GetUtcNow().UtcDateTime;
                message.LastError = "Storage file can no longer be claimed.";
                message.NextAttemptAt = null;
            }
            else
            {
                ScheduleRetry(message, timeProvider.GetUtcNow().UtcDateTime);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ScheduleRetry(message, timeProvider.GetUtcNow().UtcDateTime);
            logger.LogWarning(ex, "Storage claim outbox message {OutboxMessageId} failed.", message.Id);
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

    private static void ScheduleRetry(StorageClaimOutboxMessage message, DateTime now)
    {
        message.LastError = "Storage claim failed and will be retried.";
        message.NextAttemptAt = now.Add(
            TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, Math.Clamp(message.Attempts, 1, 8)) * 5)));
    }
}
