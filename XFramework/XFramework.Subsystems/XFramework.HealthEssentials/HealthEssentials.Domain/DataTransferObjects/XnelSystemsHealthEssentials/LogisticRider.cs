using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class LogisticRider
    {
        public LogisticRider()
        {
            LogisticJobOrders = new HashSet<LogisticJobOrder>();
            LogisticRiderHandles = new HashSet<LogisticRiderHandle>();
            LogisticRiderTags = new HashSet<LogisticRiderTag>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long CredentialId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Remarks { get; set; }
        public short? Status { get; set; }
        public string Guid { get; set; } = null!;

        public virtual ICollection<LogisticJobOrder> LogisticJobOrders { get; set; }
        public virtual ICollection<LogisticRiderHandle> LogisticRiderHandles { get; set; }
        public virtual ICollection<LogisticRiderTag> LogisticRiderTags { get; set; }
    }
}
