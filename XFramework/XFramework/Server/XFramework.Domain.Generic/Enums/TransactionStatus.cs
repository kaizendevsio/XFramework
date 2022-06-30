using System;

namespace XFramework.Domain.Generic.Enums
{
    [Flags]
    public enum TransactionStatus : int
    {
        Pending = 0,
        Ok = 1,
        Approved = 2,
        Failed = 3,
        Canceled = 4,
        Completed = 5,
        Rejected = 6,
        Refunded = 7,
        Expired = 8,
        Reversed = 9,
        OnHold = 10,
        OnGoing = 11,
    }
}
