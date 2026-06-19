namespace Wallets.Domain.Shared.Enums;

public enum WalletOutboxStatus
{
    Pending = 0,
    Processing = 1,
    Published = 2,
    Failed = 3,
    DeadLetter = 4
}
