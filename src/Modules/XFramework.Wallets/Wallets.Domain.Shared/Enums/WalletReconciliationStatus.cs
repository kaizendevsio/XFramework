namespace Wallets.Domain.Shared.Enums;

public enum WalletReconciliationStatus
{
    Pending = 0,
    Matched = 1,
    Drifted = 2,
    MarkedReconciled = 3,
    RepairSuggested = 4,
    Failed = 5
}
