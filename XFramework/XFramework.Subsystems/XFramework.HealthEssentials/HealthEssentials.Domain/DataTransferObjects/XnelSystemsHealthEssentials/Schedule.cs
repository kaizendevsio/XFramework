using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class Schedule
    {
        public Schedule()
        {
            ConsultationJobOrders = new HashSet<ConsultationJobOrder>();
            LaboratoryJobOrders = new HashSet<LaboratoryJobOrder>();
            LogisticJobOrders = new HashSet<LogisticJobOrder>();
            PharmacyJobOrders = new HashSet<PharmacyJobOrder>();
            ScheduleTags = new HashSet<ScheduleTag>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long EntityId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public short? Status { get; set; }
        public long PriorityId { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime ExpireAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Guid { get; set; } = null!;

        public virtual ScheduleEntity Entity { get; set; } = null!;
        public virtual SchedulePriority Priority { get; set; } = null!;
        public virtual ICollection<ConsultationJobOrder> ConsultationJobOrders { get; set; }
        public virtual ICollection<LaboratoryJobOrder> LaboratoryJobOrders { get; set; }
        public virtual ICollection<LogisticJobOrder> LogisticJobOrders { get; set; }
        public virtual ICollection<PharmacyJobOrder> PharmacyJobOrders { get; set; }
        public virtual ICollection<ScheduleTag> ScheduleTags { get; set; }
    }
}
