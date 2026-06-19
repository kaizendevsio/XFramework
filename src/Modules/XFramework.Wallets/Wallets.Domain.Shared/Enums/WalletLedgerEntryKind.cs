namespace Wallets.Domain.Shared.Enums;

public enum WalletLedgerEntryKind
{
    Principal = 1,
    Fee = 2,
    Hold = 3,
    Release = 4,
    Reversal = 5,
    SystemCounterparty = 6,
    Adjustment = 7,
    Refund = 8,
    Dispute = 9,
    Chargeback = 10
}
