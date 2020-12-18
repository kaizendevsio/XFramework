using System;
using System.Collections.Generic;
using System.Text;

namespace XFramework.Domain.BO
{
   public class ExceptionResponseBO
    {
        public Exception Exception { get; set; }
        public BaseAuditBO BaseAudit { get; set; }
    }
}
