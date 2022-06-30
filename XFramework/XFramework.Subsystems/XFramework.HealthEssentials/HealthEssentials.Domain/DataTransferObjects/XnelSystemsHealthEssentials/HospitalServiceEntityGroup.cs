using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class HospitalServiceEntityGroup
    {
        public HospitalServiceEntityGroup()
        {
            HospitalServiceEntities = new HashSet<HospitalServiceEntity>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; } = null!;
        public string Guid { get; set; } = null!;

        public virtual ICollection<HospitalServiceEntity> HospitalServiceEntities { get; set; }
    }
}
