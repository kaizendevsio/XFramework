namespace Wallets.Api.Events;

/// <summary>
/// Published when a transaction is reversed.
/// </summary>
public record TransactionReversedEvent : WalletEvent
{
    public Guid OriginalTransactionId { get; init; }
    public Guid ReversalTransactionId { get; init; }
    public decimal Amount { get; init; }
}
