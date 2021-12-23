using System;

#nullable disable

namespace RBS.Domain.DataTransferObjects
{
    public partial class TblApplicationLog
    {
        public long Id { get; set; }
        public long? AppId { get; set; }
        public short? Severity { get; set; }
        public string Message { get; set; }
        public DateTime? CreatedOn { get; set; }
        public bool IsDeleted { get; set; }
        public string Initiator { get; set; }

        public virtual TblApplication App { get; set; }
    }
}
