using MemoryPack;

namespace Bolt.Domain.Shared.Contracts.ServiceDiscovery;

[MemoryPackable]
public partial class BoltServiceManifest
{
    [MemoryPackOrder(0)]
    public string ServiceName { get; set; } = string.Empty;

    [MemoryPackOrder(1)]
    public string DisplayName { get; set; } = string.Empty;

    [MemoryPackOrder(2)]
    public string Description { get; set; } = string.Empty;

    [MemoryPackOrder(3)]
    public string? Version { get; set; }

    [MemoryPackOrder(4)]
    public string? HealthUrl { get; set; }

    [MemoryPackOrder(5)]
    public List<BoltModuleManifest> Modules { get; set; } = [];

    [MemoryPackOrder(6)]
    public List<BoltDependencyRequirement> Dependencies { get; set; } = [];

    [MemoryPackOrder(7)]
    public Dictionary<string, string> Metadata { get; set; } = [];
}
