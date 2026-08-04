using Wallets.Api.Services;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

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
                IReadOnlyList<Guid> tenantIds;
                await using (var discoveryScope = scopeFactory.CreateAsyncScope())
                {
                    var discoveryAuthorization = await discoveryScope.ServiceProvider
                        .GetRequiredService<ITrustedServiceTargetContextInitializer>()
                        .EstablishTenantlessAsync(
                            XFrameworkServiceNames.Wallets,
                            [
                                XFrameworkServiceScopes.WalletsAdmin,
                                XFrameworkServiceScopes.DataContextQueryAllTenants
                            ],
                            XFrameworkServiceNames.Wallets,
                            ct: stoppingToken);
                    if (!discoveryAuthorization.IsSuccess)
                    {
                        logger.LogWarning(
                            "Wallet outbox tenant discovery authorization failed: {Error}",
                            discoveryAuthorization.Error);
                        await timer.WaitForNextTickAsync(stoppingToken);
                        continue;
                    }

                    tenantIds = await discoveryScope.ServiceProvider
                        .GetRequiredService<IWalletOutboxService>()
                        .GetDueTenantIdsAsync(stoppingToken);
                }

                foreach (var tenantId in tenantIds)
                {
                    await using var tenantScope = scopeFactory.CreateAsyncScope();
                    var authorization = await tenantScope.ServiceProvider
                        .GetRequiredService<ITrustedServiceTargetContextInitializer>()
                        .EstablishAsync(
                            tenantId,
                            XFrameworkServiceNames.Wallets,
                            [XFrameworkServiceScopes.WalletsAdmin],
                            XFrameworkServiceNames.Wallets,
                            ct: stoppingToken);
                    if (!authorization.IsSuccess)
                    {
                        logger.LogWarning(
                            "Wallet outbox tenant {TenantId} authorization failed: {Error}",
                            tenantId,
                            authorization.Error);
                        continue;
                    }

                    await tenantScope.ServiceProvider
                        .GetRequiredService<IWalletOutboxService>()
                        .DispatchDueAsync(stoppingToken);
                }
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
