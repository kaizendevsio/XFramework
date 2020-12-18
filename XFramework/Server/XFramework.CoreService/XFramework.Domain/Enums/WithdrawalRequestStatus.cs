using System;
using System.Collections.Generic;
using System.Text;

namespace XFramework.Domain.Enums
{
    public enum WithdrawalRequestStatus : byte
    {
        None = 0,
        Pending = 1,
        Completed = 2,
        Rejected = 10
    }

}
