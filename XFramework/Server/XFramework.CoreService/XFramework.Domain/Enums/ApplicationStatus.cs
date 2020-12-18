using System;
using System.Collections.Generic;
using System.Text;

namespace XFramework.Domain.Enums
{
    [Flags]
    public enum ApplicationStatus
    {
        Disabled = 0,
        Active = 1,
        Suspended = 2
    }
}
