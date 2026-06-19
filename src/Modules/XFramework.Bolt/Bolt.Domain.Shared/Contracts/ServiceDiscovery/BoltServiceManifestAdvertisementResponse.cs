using MemoryPack;

namespace Bolt.Domain.Shared.Contracts.ServiceDiscovery;

[MemoryPackable]
public partial class BoltServiceManifestAdvertisementResponse
{
    [MemoryPackOrder(0)]
    public bool Accepted { get; set; }

    [MemoryPackOrder(1)]
    public string Message { get; set; } = string.Empty;
}
