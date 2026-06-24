using Messaging.Api.Services;

namespace Messaging.Api.HostedService;

public sealed class MessagingOutboxDispatcherHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<MessagingOutboxDispatcherHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

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
                logger.LogError(ex, "Messaging outbox dispatcher failed");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagingRealtimePublisher>();
        var notificationFanout = scope.ServiceProvider.GetRequiredService<IMessagingNotificationFanout>();

        var due = await db.Set<MessageOutboxEvent>()
            .IgnoreQueryFilters()
            .AsTracking()
            .Where(e => !e.IsDeleted && e.IsEnabled)
            .Where(e => e.ProcessedAt == null)
            .Where(e => e.Attempts < 5)
            .OrderBy(e => e.OccurredAt)
            .Take(100)
            .ToListAsync(ct);

        foreach (var outboxEvent in due)
        {
            try
            {
                await publisher.PublishAsync(outboxEvent, ct);
                await notificationFanout.CreateNotificationsAsync(outboxEvent, ct);
                outboxEvent.ProcessedAt = DateTime.UtcNow;
                outboxEvent.LastError = null;
            }
            catch (Exception ex)
            {
                outboxEvent.Attempts++;
                outboxEvent.LastError = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
                logger.LogWarning(ex, "Messaging outbox event {OutboxEventId} publish failed", outboxEvent.Id);
            }
        }

        if (due.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
