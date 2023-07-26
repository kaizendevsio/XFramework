using System;

namespace XFramework.Domain.Generic.Enums;

[Flags]
public enum WalletType : int
{
    CurrencyValue = 1,
    NetworkCounter = 2,
    VirtualValue = 3
}