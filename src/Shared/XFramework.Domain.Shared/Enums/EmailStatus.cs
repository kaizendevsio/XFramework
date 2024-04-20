﻿namespace XFramework.Domain.Shared.Enums;

[Flags]
public enum EmailStatus : short
{
    Empty = 0,
    Unverified = 1,
    Verified = 2,
    Blocklisted = 3,
    Rejected = 4,
    NonExistent = 5
}