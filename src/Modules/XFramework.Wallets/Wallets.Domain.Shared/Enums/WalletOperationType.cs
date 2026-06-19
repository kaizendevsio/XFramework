namespace Wallets.Domain.Shared.Enums;

public enum WalletOperationType
{
    Unknown = 0,
    Credit = 1,
    Debit = 2,
    Transfer = 3,
    Conversion = 4,
    Hold = 5,
    Release = 6,
    Reversal = 7,
    Refund = 8,
    DepositApproval = 9,
    WithdrawalApproval = 10,
    Batch = 11
}
