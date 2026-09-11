using Communications.Api.Services;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace Communications.Api.HostedService;

public sealed class CommunicationsOutboxDispatcherHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<CommunicationsOutboxDispatcherHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(45);
    private const int BatchSize = 100;
    private const int MaxAttempts = 8;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Communications outbox dispatcher failed");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        List<OutboxCandidate> candidates;
        await using (var discoveryScope = scopeFactory.CreateAsyncScope())
        {
            var discoveryAuthorization = await discoveryScope.ServiceProvider
                .GetRequiredService<ITrustedServiceTargetContextInitializer>()
                .EstablishTenantlessAsync(
                    XFrameworkServiceNames.Communications,
                    [XFrameworkServiceScopes.DataContextQueryAllTenants],
                    XFrameworkServiceNames.Communications,
                    ct: ct);
            if (!discoveryAuthorization.IsSuccess)
            {
                logger.LogWarning(
                    "Communications outbox tenant discovery authorization failed: {Error}",
                    discoveryAuthorization.Error);
                return;
            }

            var discoveryDb = discoveryScope.ServiceProvider.GetRequiredService<DbContext>();
            candidates = await discoveryDb.Set<MessageOutboxEvent>()
                .IgnoreQueryFilters()
                .Where(e => !e.IsDeleted && e.IsEnabled)
                .Where(e => e.ProcessedAt == null)
                .Where(e => e.DeadLetteredAt == null)
                .Where(e => e.NextAttemptAt == null || e.NextAttemptAt <= now)
                .Where(e => e.LeaseExpiresAt == null || e.LeaseExpiresAt <= now)
                .OrderBy(e => e.OccurredAt)
                .Take(BatchSize)
                .Select(e => new OutboxCandidate(e.Id, e.TenantId))
                .ToListAsync(ct);
        }

        foreach (var tenantCandidates in candidates.GroupBy(candidate => candidate.TenantId))
        {
            await DispatchTenantBatchAsync(
                tenantCandidates.Key,
                tenantCandidates.Select(candidate => candidate.Id).ToArray(),
                now,
                ct);
        }
    }

    private async Task DispatchTenantBatchAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> candidateIds,
        DateTime now,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var authorization = await scope.ServiceProvider
            .GetRequiredService<ITrustedServiceTargetContextInitializer>()
            .EstablishAsync(
                tenantId,
                XFrameworkServiceNames.Communications,
                [XFrameworkServiceScopes.CommunicationsAdmin],
                XFrameworkServiceNames.Communications,
                ct: ct);
        if (!authorization.IsSuccess)
        {
            logger.LogWarning(
                "Communications outbox tenant {TenantId} authorization failed: {Error}",
                tenantId,
                authorization.Error);
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<ICommunicationsRealtimePublisher>();
        var notificationFanout = scope.ServiceProvider.GetRequiredService<ICommunicationsNotificationFanout>();
        var leaseOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";

        var leaseExpiresAt = now.Add(LeaseDuration);
        await db.Set<MessageOutboxEvent>()
            .IgnoreQueryFilters()
            .Where(e => candidateIds.Contains(e.Id))
            .Where(e => e.TenantId == tenantId)
            .Where(e => e.ProcessedAt == null)
            .Where(e => e.DeadLetteredAt == null)
            .Where(e => e.LeaseExpiresAt == null || e.LeaseExpiresAt <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.LeaseOwner, leaseOwner)
                .SetProperty(e => e.LeaseExpiresAt, leaseExpiresAt)
                .SetProperty(e => e.ModifiedAt, now), ct);

        var due = await db.Set<MessageOutboxEvent>()
            .IgnoreQueryFilters()
            .AsTracking()
            .Where(e => candidateIds.Contains(e.Id))
            .Where(e => e.TenantId == tenantId)
            .Where(e => e.LeaseOwner == leaseOwner)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(ct);

        foreach (var outboxEvent in due)
        {
            var attemptStartedAt = DateTime.UtcNow;
            try
            {
                if (outboxEvent.RealtimeProcessedAt is null)
                {
                    outboxEvent.RealtimeAttempts++;
                    await publisher.PublishAsync(outboxEvent, ct);
                    outboxEvent.RealtimeProcessedAt = DateTime.UtcNow;
                }

                if (outboxEvent.NotificationProcessedAt is null)
                {
                    outboxEvent.NotificationAttempts++;
                    await notificationFanout.CreateNotificationsAsync(outboxEvent, ct);
                    outboxEvent.NotificationProcessedAt = DateTime.UtcNow;
                }

                outboxEvent.ProcessedAt = DateTime.UtcNow;
                outboxEvent.LastError = null;
                outboxEvent.NextAttemptAt = null;
            }
            catch (Exception ex)
            {
                outboxEvent.Attempts++;
                outboxEvent.LastAttemptAt = attemptStartedAt;
                outboxEvent.LastError = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
                if (outboxEvent.Attempts >= MaxAttempts)
                {
                    outboxEvent.DeadLetteredAt = DateTime.UtcNow;
                    outboxEvent.NextAttemptAt = null;
                }
                else
                {
                    outboxEvent.NextAttemptAt = DateTime.UtcNow.Add(CalculateBackoff(outboxEvent.Attempts));
                }

                logger.LogWarning(ex, "Communications outbox event {OutboxEventId} publish failed", outboxEvent.Id);
            }
            finally
            {
                outboxEvent.LeaseOwner = null;
                outboxEvent.LeaseExpiresAt = null;
                outboxEvent.ModifiedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private sealed record OutboxCandidate(Guid Id, Guid TenantId);

    private static TimeSpan CalculateBackoff(int attempts)
    {
        var boundedAttempts = Math.Clamp(attempts, 1, 6);
        return TimeSpan.FromSeconds(Math.Pow(2, boundedAttempts) * 5);
    }
}
