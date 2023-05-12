using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class Log
    {
        public long Id { get; set; }
        public long? ApplicationId { get; set; }
        public short? Severity { get; set; }
        public string Message { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public string Initiator { get; set; }
        public short? Type { get; set; }
        public string Name { get; set; }
        public bool? Seen { get; set; }
        public string Guid { get; set; }

        public virtual Application Application { get; set; }
    }
}
