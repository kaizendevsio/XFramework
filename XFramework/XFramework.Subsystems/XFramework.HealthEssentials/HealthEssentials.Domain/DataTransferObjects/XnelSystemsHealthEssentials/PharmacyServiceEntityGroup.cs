using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class PharmacyServiceEntityGroup
    {
        public PharmacyServiceEntityGroup()
        {
            PharmacyServiceEntities = new HashSet<PharmacyServiceEntity>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; } = null!;
        public string Guid { get; set; } = null!;

        public virtual ICollection<PharmacyServiceEntity> PharmacyServiceEntities { get; set; }
    }
}
