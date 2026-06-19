using MemoryPack;

namespace Bolt.Domain.Shared.Contracts.ServiceDiscovery;

[MemoryPackable]
public partial class BoltModuleRegistryResponse
{
    [MemoryPackOrder(0)]
    public List<BoltModuleRegistryItem> Modules { get; set; } = [];
}
