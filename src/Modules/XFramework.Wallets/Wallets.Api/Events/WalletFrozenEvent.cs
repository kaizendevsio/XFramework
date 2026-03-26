namespace Wallets.Api.Events;

/// <summary>
/// Published when a wallet is frozen, preventing all financial operations.
/// </summary>
public record WalletFrozenEvent : WalletEvent
{
    public string? Reason { get; init; }
}
