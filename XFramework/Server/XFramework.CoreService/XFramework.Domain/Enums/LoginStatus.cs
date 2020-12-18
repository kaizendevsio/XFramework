using System;
using System.Collections.Generic;
using System.Text;

namespace XFramework.Domain.Enums
{
    [Flags]
   public enum LoginStatus : int
    {
        Disabled = 0,
        Enabled = 1,
        Restricted = 2,
        Blocked = 3
    }
}
