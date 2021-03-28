using System;

namespace XFramework.Domain.BusinessObjects
{
   public class ExceptionResponseBO
    {
        public Exception Exception { get; set; }
        public BaseAuditBO BaseAudit { get; set; }
    }
}
