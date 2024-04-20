namespace XFramework.Domain.Shared.Enums;

[Flags]
public enum WalletType : int
{
    CurrencyValue = 1,
    NetworkCounter = 2,
    VirtualValue = 3
}