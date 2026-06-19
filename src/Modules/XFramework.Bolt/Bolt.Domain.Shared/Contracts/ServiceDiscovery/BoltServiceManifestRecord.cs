using XFramework.Domain.Shared.Contracts.Base;

namespace Bolt.Domain.Shared.Contracts.ServiceDiscovery;

public sealed class BoltServiceManifestRecord : IAuditable
{
    public Guid Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Version { get; set; }
    public bool IsConnected { get; set; }
    public int ConnectionCount { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime? LastConnectedAt { get; set; }
    public DateTime? LastDisconnectedAt { get; set; }
    public string ManifestHash { get; set; } = string.Empty;
    public string ManifestJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
