using MemoryPack;

namespace Bolt.Domain.Shared.Contracts.ServiceDiscovery;

[MemoryPackable]
public partial class BoltDependencyStatus
{
    [MemoryPackOrder(0)]
    public BoltDependencyRequirement Requirement { get; set; } = new();

    [MemoryPackOrder(1)]
    public bool IsSatisfied { get; set; }

    [MemoryPackOrder(2)]
    public string? MatchedKey { get; set; }

    [MemoryPackOrder(3)]
    public string Message { get; set; } = string.Empty;
}
