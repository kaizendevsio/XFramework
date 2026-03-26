namespace Wallets.Api.Events;

/// <summary>
/// Base event record for all wallet domain events.
/// </summary>
public record WalletEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType { get; init; } = string.Empty;
    public Guid WalletId { get; init; }
    public Guid CredentialId { get; init; }
    public Guid TenantId { get; init; }
}
