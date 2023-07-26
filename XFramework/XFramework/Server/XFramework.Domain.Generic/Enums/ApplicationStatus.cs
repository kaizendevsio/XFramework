using System;

namespace XFramework.Domain.Generic.Enums;

[Flags]
public enum ApplicationStatus
{
    Disabled = 0,
    Active = 1,
    Suspended = 2
}