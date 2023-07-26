using System;

namespace XFramework.Domain.Generic.Enums;

[Flags]
public enum IncomeDistributionType: int
{
    Fixed = 0,
    Percentage = 1
}