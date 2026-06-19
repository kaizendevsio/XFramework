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
    Batch = 11,
    DisputeHold = 12,
    DisputeResolution = 13,
    Chargeback = 14,
    ManualAdjustment = 15,
    Freeze = 16,
    Unfreeze = 17,
    Close = 18
}
