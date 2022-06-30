using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class ScheduleEntity
    {
        public ScheduleEntity()
        {
            Availabilities = new HashSet<Availability>();
            Schedules = new HashSet<Schedule>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string Guid { get; set; } = null!;
        public long GroupId { get; set; }
        public int? SortOrder { get; set; }

        public virtual ScheduleEntityGroup Group { get; set; } = null!;
        public virtual ICollection<Availability> Availabilities { get; set; }
        public virtual ICollection<Schedule> Schedules { get; set; }
    }
}
