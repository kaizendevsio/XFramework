using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class SchedulePriorityEntity
    {
        public SchedulePriorityEntity()
        {
            SchedulePriorities = new HashSet<SchedulePriority>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string Guid { get; set; } = null!;
        public int? SortOrder { get; set; }

        public virtual ICollection<SchedulePriority> SchedulePriorities { get; set; }
    }
}
