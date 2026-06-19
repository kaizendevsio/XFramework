using MemoryPack;

namespace Bolt.Domain.Shared.Contracts.ServiceDiscovery;

[MemoryPackable]
public partial class BoltModuleManifest
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
    public List<BoltTenantModuleFeatureManifest> Features { get; set; } = [];

    [MemoryPackOrder(6)]
    public List<BoltDependencyRequirement> Dependencies { get; set; } = [];
}
