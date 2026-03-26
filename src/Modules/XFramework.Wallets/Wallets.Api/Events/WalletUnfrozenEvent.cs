namespace Wallets.Api.Events;

/// <summary>
/// Published when a wallet is unfrozen and restored to active status.
/// </summary>
public record WalletUnfrozenEvent : WalletEvent;
