namespace Wallets.Api.Events;

/// <summary>
/// Published when a transaction amount exceeds the configured threshold.
/// </summary>
public record LargeTransactionEvent : WalletEvent
{
    public decimal Amount { get; init; }
    public string TransactionType { get; init; } = string.Empty;
    public decimal Threshold { get; init; }
}
