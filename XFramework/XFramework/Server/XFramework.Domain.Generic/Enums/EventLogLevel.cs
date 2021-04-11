using System;

namespace XFramework.Domain.Generic.Enums
{
    [Flags]
    public enum SeverityLevel
    {
        None = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        FatalError = 4,
        SecurityWarning = 5
    }
}
