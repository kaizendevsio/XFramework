using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DataTableObjects
{
    public partial class TblLog
    {
        public long Id { get; set; }
        public long? ApplicationId { get; set; }
        public short? Severity { get; set; }
        public string Message { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public string Initiator { get; set; }

        public virtual TblApplication Application { get; set; }
    }
}
