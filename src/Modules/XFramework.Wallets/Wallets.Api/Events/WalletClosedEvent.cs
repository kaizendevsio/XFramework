namespace Wallets.Api.Events;

/// <summary>
/// Published when an empty wallet is closed.
/// </summary>
public record WalletClosedEvent : WalletEvent
{
    public string? Reason { get; init; }
}
