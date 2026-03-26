namespace Wallets.Api.Events;

/// <summary>
/// Published when a wallet transaction (credit or debit) completes successfully.
/// </summary>
public record TransactionCompletedEvent : WalletEvent
{
    public decimal Amount { get; init; }
    public string TransactionType { get; init; } = string.Empty;
    public string? ReferenceNumber { get; init; }
    public decimal RunningBalance { get; init; }
}
