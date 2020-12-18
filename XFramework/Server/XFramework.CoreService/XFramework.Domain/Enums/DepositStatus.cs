using System;
using System.Collections.Generic;
using System.Text;

namespace XFramework.Domain.Enums
{
    [Flags]
    public enum DepositStatus : int
    {
        PendingPayment = 0,
        UnPaid = 1,
        UnderPaid = 2,
        Paid = 3,
        Expired = 4,
        InvalidPayment = 5,
        Blocked = 6,
        Revoked = 7,
        Cancelled = 8
    }
}
