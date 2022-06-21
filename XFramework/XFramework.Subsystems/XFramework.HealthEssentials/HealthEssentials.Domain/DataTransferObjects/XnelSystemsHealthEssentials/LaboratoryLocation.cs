using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class LaboratoryLocation
    {
        public LaboratoryLocation()
        {
            ConsultationJobOrderLaboratories = new HashSet<ConsultationJobOrderLaboratory>();
            LaboratoryJobOrders = new HashSet<LaboratoryJobOrder>();
            LaboratoryLocationTags = new HashSet<LaboratoryLocationTag>();
            LaboratoryServices = new HashSet<LaboratoryService>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long LaboratoryId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? UnitNumber { get; set; }
        public string? Street { get; set; }
        public string? Building { get; set; }
        public long? Barangay { get; set; }
        public long? City { get; set; }
        public string? Subdivision { get; set; }
        public long? Region { get; set; }
        public bool? MainAddress { get; set; }
        public long? Province { get; set; }
        public long? Country { get; set; }
        public string Guid { get; set; } = null!;
        public int? Status { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }
        public string? AlternativePhone { get; set; }

        public virtual Laboratory Laboratory { get; set; } = null!;
        public virtual ICollection<ConsultationJobOrderLaboratory> ConsultationJobOrderLaboratories { get; set; }
        public virtual ICollection<LaboratoryJobOrder> LaboratoryJobOrders { get; set; }
        public virtual ICollection<LaboratoryLocationTag> LaboratoryLocationTags { get; set; }
        public virtual ICollection<LaboratoryService> LaboratoryServices { get; set; }
    }
}
