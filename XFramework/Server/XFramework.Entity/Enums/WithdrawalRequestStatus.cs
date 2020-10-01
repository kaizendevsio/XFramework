using System;
using System.Collections.Generic;
using System.Text;

namespace XFramework.Data.Enums
{
    public enum WithdrawalRequestStatus : byte
    {
        None = 0,
        Pending = 1,
        Completed = 2,
        Rejected = 10
    }

}
