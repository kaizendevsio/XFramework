namespace Wallets.Domain.Shared.Enums;

public enum WalletWorkflowStatus
{
    Draft = 0,
    PendingValidation = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    Settling = 5,
    Completed = 6,
    Failed = 7,
    Cancelled = 8,
    Expired = 9
}
