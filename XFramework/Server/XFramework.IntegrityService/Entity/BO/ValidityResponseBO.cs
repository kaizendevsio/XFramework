using System;
using System.Collections.Generic;
using System.Text;
using XFramework.Integrity.Entity.Enums;

namespace XFramework.Integrity.Entity.BO
{
   public class ValidityResponseBO
    {
        public ValidityState ValidityState { get; set; }
        public DateTime ValidUntil { get; set; }
        public string Uid { get; set; }
        public string AppSystemName { get; set; }
    }
}
