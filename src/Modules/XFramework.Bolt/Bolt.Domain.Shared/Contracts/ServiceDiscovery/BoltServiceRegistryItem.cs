using MemoryPack;

namespace Bolt.Domain.Shared.Contracts.ServiceDiscovery;

[MemoryPackable]
public partial class BoltServiceRegistryItem
{
    [MemoryPackOrder(0)]
    public string ClientId { get; set; } = string.Empty;

    [MemoryPackOrder(1)]
    public string ClientName { get; set; } = string.Empty;

    [MemoryPackOrder(2)]
    public string ServiceName { get; set; } = string.Empty;

    [MemoryPackOrder(3)]
    public string DisplayName { get; set; } = string.Empty;

    [MemoryPackOrder(4)]
    public string? Version { get; set; }

    [MemoryPackOrder(5)]
    public BoltRegistryStatus Status { get; set; }

    [MemoryPackOrder(6)]
    public int ConnectionCount { get; set; }

    [MemoryPackOrder(7)]
    public DateTime LastSeenAt { get; set; }

    [MemoryPackOrder(8)]
    public DateTime? LastConnectedAt { get; set; }

    [MemoryPackOrder(9)]
    public DateTime? LastDisconnectedAt { get; set; }

    [MemoryPackOrder(10)]
    public BoltServiceManifest Manifest { get; set; } = new();

    [MemoryPackOrder(11)]
    public List<BoltDependencyStatus> DependencyStatuses { get; set; } = [];
}
