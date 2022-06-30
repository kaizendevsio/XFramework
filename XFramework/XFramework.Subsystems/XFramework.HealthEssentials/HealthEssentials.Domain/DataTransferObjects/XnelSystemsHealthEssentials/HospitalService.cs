using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class HospitalService
    {
        public HospitalService()
        {
            HospitalServiceTags = new HashSet<HospitalServiceTag>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long EntityId { get; set; }
        public long HospitalLocationId { get; set; }
        public long HospitalId { get; set; }
        public string? Name { get; set; }
        public string? ShortName { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public decimal? MaxDiscount { get; set; }
        public decimal? Quantity { get; set; }
        public long? UnitId { get; set; }
        public string Guid { get; set; } = null!;

        public virtual HospitalServiceEntity Entity { get; set; } = null!;
        public virtual Hospital Hospital { get; set; } = null!;
        public virtual HospitalLocation HospitalLocation { get; set; } = null!;
        public virtual Unit? Unit { get; set; }
        public virtual ICollection<HospitalServiceTag> HospitalServiceTags { get; set; }
    }
}
