using Bolt.Domain.Shared.Contracts.ServiceDiscovery;

namespace XFramework.Integration.ServiceDiscovery;

public interface IBoltServiceManifestProvider
{
    ValueTask<BoltServiceManifest?> GetManifestAsync(CancellationToken ct = default);
}
