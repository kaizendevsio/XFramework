using Wallets.Api.Services;

namespace Wallets.Api.HostedService;

public sealed class WalletOutboxDispatcherHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<WalletOutboxDispatcherHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IWalletOutboxService>();
                await service.DispatchDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Wallet outbox dispatcher failed");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
