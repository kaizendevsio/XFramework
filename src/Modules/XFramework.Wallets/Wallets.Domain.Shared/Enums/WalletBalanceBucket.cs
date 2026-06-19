namespace Wallets.Domain.Shared.Enums;

public enum WalletBalanceBucket
{
    External = 0,
    Available = 1,
    DebitHold = 2,
    CreditHold = 3,
    Fee = 4
}
