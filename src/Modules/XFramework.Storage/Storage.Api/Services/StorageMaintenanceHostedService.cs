using Microsoft.Extensions.Options;

namespace Storage.Api.Services;

public sealed class StorageMaintenanceHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<StorageOptions> options,
    TimeProvider timeProvider,
    ILogger<StorageMaintenanceHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(Math.Clamp(options.Value.MaintenancePollSeconds, 5, 3600));
        using var timer = new PeriodicTimer(pollInterval, timeProvider);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<StorageMaintenanceService>()
                    .RunBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Storage maintenance poll failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
