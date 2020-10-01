using System;
using System.Collections.Generic;
using System.Text;
using XFramework.Data.DTO;

namespace XFramework.Data.BO
{
   public class BaseAuditBO
    {
        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public string AppUID { get; set; }
        public long AppId { get; set; }
        

    }
}
