using MemoryPack;

namespace Bolt.Domain.Shared.Contracts.ServiceDiscovery;

[MemoryPackable]
public partial class BoltDependencyRequirement
{
    [MemoryPackOrder(0)]
    public BoltDependencyKind Kind { get; set; }

    [MemoryPackOrder(1)]
    public string Key { get; set; } = string.Empty;

    [MemoryPackOrder(2)]
    public string? DisplayName { get; set; }

    [MemoryPackOrder(3)]
    public string? MinVersion { get; set; }

    [MemoryPackOrder(4)]
    public bool Required { get; set; } = true;
}
