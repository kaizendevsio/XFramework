using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace Notifications.Api.Services;

/// <summary>
/// Drains queued delivery jobs.
///
/// Every delivery row is tenant-owned and AppDbContext fails closed without a trusted tenant
/// context, so this loop cannot just query: a background scope has no tenant of its own. It
/// discovers the tenants that have work under a tenantless cross-tenant context, then re-enters
/// once per tenant with that tenant established so the global query filters resolve and writes
/// are allowed. Without this the very first query threw on every tick and no queued push, SMS,
/// email or webhook ever left the table.
/// </summary>
public sealed class NotificationDeliveryDispatcherHostedService(
    IServiceScopeFactory scopeFactory,
    NotificationDeliverySignal signal,
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
                await DispatchDueTenantsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification delivery dispatcher failed");
            }

            await signal.WaitAsync(_interval, stoppingToken);
        }
    }

    private async Task DispatchDueTenantsAsync(CancellationToken ct)
    {
        IReadOnlyList<Guid> tenantIds;
        await using (var discoveryScope = scopeFactory.CreateAsyncScope())
        {
            var discoveryAuthorization = await discoveryScope.ServiceProvider
                .GetRequiredService<ITrustedServiceTargetContextInitializer>()
                .EstablishTenantlessAsync(
                    XFrameworkServiceNames.Notifications,
                    [XFrameworkServiceScopes.DataContextQueryAllTenants],
                    XFrameworkServiceNames.Notifications,
                    ct: ct);
            if (!discoveryAuthorization.IsSuccess)
            {
                logger.LogWarning(
                    "Notification delivery tenant discovery authorization failed: {Error}",
                    discoveryAuthorization.Error);
                return;
            }

            tenantIds = await discoveryScope.ServiceProvider
                .GetRequiredService<NotificationDeliveryDispatcher>()
                .FindDueTenantIdsAsync(ct);
        }

        foreach (var tenantId in tenantIds)
        {
            ct.ThrowIfCancellationRequested();
            await using var tenantScope = scopeFactory.CreateAsyncScope();

            // Only tenant.target: the dispatcher reads and writes its own module's schema in
            // process and needs nothing beyond permission to act for this tenant.
            var authorization = await tenantScope.ServiceProvider
                .GetRequiredService<ITrustedServiceTargetContextInitializer>()
                .EstablishAsync(
                    tenantId,
                    XFrameworkServiceNames.Notifications,
                    [XFrameworkServiceScopes.TenantTarget],
                    XFrameworkServiceNames.Notifications,
                    ct: ct);
            if (!authorization.IsSuccess)
            {
                logger.LogWarning(
                    "Notification delivery tenant {TenantId} authorization failed: {Error}",
                    tenantId,
                    authorization.Error);
                continue;
            }

            await tenantScope.ServiceProvider
                .GetRequiredService<NotificationDeliveryDispatcher>()
                .DispatchDueAsync(ct);
        }

        // A full discovery page means more tenants are waiting; come back without sleeping.
        if (tenantIds.Count >= NotificationDeliveryDispatcher.TenantDiscoveryLimit)
            signal.Notify();
    }
}
