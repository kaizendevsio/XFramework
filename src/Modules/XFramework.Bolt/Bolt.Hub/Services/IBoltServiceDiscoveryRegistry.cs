using Bolt.Domain.Shared.Contracts.ServiceDiscovery;
using Bolt.Server;

namespace Bolt.Hub.Services;

public interface IBoltServiceDiscoveryRegistry
{
    Task ResetPresenceAsync(CancellationToken ct);

    Task<BoltServiceManifestAdvertisementResponse> AdvertiseAsync(
        BoltRequestContext context,
        BoltServiceManifest manifest,
        CancellationToken ct);

    Task MarkConnectedAsync(BoltClientConnectionEvent connectionEvent, CancellationToken ct);

    Task MarkDisconnectedAsync(BoltClientConnectionEvent connectionEvent, CancellationToken ct);

    Task<BoltServiceRegistryResponse> GetServicesAsync(BoltServiceRegistryRequest request, CancellationToken ct);

    Task<BoltModuleRegistryResponse> GetModulesAsync(BoltModuleRegistryRequest request, CancellationToken ct);
}
