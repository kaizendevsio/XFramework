using MemoryPack;

namespace Bolt.Domain.Shared.Contracts.ServiceDiscovery;

[MemoryPackable]
public partial class BoltServiceRegistryResponse
{
    [MemoryPackOrder(0)]
    public List<BoltServiceRegistryItem> Services { get; set; } = [];
}
