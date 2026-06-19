using MemoryPack;

namespace Bolt.Domain.Shared.Contracts.ServiceDiscovery;

[MemoryPackable]
public partial class BoltTenantModuleFeatureManifest
{
    [MemoryPackOrder(0)]
    public string? Key { get; set; }

    [MemoryPackOrder(1)]
    public string ModuleKey { get; set; } = string.Empty;

    [MemoryPackOrder(2)]
    public string SubFeatureKey { get; set; } = string.Empty;

    [MemoryPackOrder(3)]
    public string DisplayName { get; set; } = string.Empty;

    [MemoryPackOrder(4)]
    public string Description { get; set; } = string.Empty;

    [MemoryPackOrder(5)]
    public string IconName { get; set; } = "box";

    [MemoryPackOrder(6)]
    public bool DefaultEnabled { get; set; } = true;

    [MemoryPackOrder(7)]
    public List<BoltDependencyRequirement> Dependencies { get; set; } = [];
}
