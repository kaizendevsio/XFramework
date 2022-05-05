using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class Logistic
    {
        public Logistic()
        {
            LogisticRiderHandles = new HashSet<LogisticRiderHandle>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long EntityId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Remarks { get; set; }
        public string Guid { get; set; } = null!;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }
        public string? Logo { get; set; }

        public virtual LogisticEntity Entity { get; set; } = null!;
        public virtual ICollection<LogisticRiderHandle> LogisticRiderHandles { get; set; }
    }
}
