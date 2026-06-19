using MemoryPack;

namespace Bolt.Domain.Shared.Contracts.ServiceDiscovery;

[MemoryPackable]
public partial class BoltModuleRegistryRequest
{
    [MemoryPackOrder(0)]
    public bool IncludeOffline { get; set; } = true;
}
