namespace Wallets.Domain.Shared.Enums;

public enum WalletOperationStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Rejected = 3,
    Reversed = 4
}
