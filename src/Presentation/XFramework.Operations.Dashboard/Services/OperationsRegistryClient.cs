using Bolt.Client;
using Bolt.Domain.Shared.Contracts.ServiceDiscovery;

namespace XFramework.Operations.Dashboard.Services;

public sealed class OperationsRegistryClient(
    BoltClient client,
    ILogger<OperationsRegistryClient> logger)
{
    public async Task<DashboardRegistrySnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        if (!client.IsConnected)
        {
            return DashboardRegistrySnapshot.EmptyDisconnected("Bolt Hub is not connected.");
        }

        try
        {
            var services = await client.SendAsync<BoltServiceRegistryRequest, BoltServiceRegistryResponse>(
                string.Empty,
                BoltServiceDiscoveryCommands.GetServiceRegistry,
                new BoltServiceRegistryRequest { IncludeOffline = true },
                ct);

            var modules = await client.SendAsync<BoltModuleRegistryRequest, BoltModuleRegistryResponse>(
                string.Empty,
                BoltServiceDiscoveryCommands.GetModuleRegistry,
                new BoltModuleRegistryRequest { IncludeOffline = true },
                ct);

            var snapshot = OperationsRegistryMapper.CreateSnapshot(
                services?.Services ?? [],
                modules?.Modules ?? [],
                DateTimeOffset.UtcNow);

            return snapshot;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read Bolt service registry");
            return DashboardRegistrySnapshot.EmptyDisconnected("Bolt service registry is unavailable.");
        }
    }
}
