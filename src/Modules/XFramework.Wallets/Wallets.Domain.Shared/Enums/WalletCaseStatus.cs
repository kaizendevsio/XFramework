namespace Wallets.Domain.Shared.Enums;

public enum WalletCaseStatus
{
    Open = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    OnHold = 4,
    Resolved = 5,
    Failed = 6,
    Cancelled = 7
}
