namespace Notifications.Api.Services;

public sealed class NotificationDeliveryDispatcherHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationDeliveryDispatcherHostedService> logger,
    IConfiguration configuration) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(
        Math.Max(5, configuration.GetValue("Notifications:Delivery:PollSeconds", 15)));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDeliveryDispatcher>();
                await dispatcher.DispatchDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification delivery dispatcher failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
