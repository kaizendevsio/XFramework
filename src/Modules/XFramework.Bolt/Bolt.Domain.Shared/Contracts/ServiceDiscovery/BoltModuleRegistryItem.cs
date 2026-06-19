using MemoryPack;

namespace Bolt.Domain.Shared.Contracts.ServiceDiscovery;

[MemoryPackable]
public partial class BoltModuleRegistryItem
{
    [MemoryPackOrder(0)]
    public string ModuleKey { get; set; } = string.Empty;

    [MemoryPackOrder(1)]
    public string DisplayName { get; set; } = string.Empty;

    [MemoryPackOrder(2)]
    public string Description { get; set; } = string.Empty;

    [MemoryPackOrder(3)]
    public string? Version { get; set; }

    [MemoryPackOrder(4)]
    public string IconName { get; set; } = "box";

    [MemoryPackOrder(5)]
    public string ServiceName { get; set; } = string.Empty;

    [MemoryPackOrder(6)]
    public string ClientId { get; set; } = string.Empty;

    [MemoryPackOrder(7)]
    public string ClientName { get; set; } = string.Empty;

    [MemoryPackOrder(8)]
    public BoltRegistryStatus Status { get; set; }

    [MemoryPackOrder(9)]
    public List<BoltTenantModuleFeatureRegistryItem> Features { get; set; } = [];

    [MemoryPackOrder(10)]
    public List<BoltDependencyStatus> DependencyStatuses { get; set; } = [];
}
