using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class Hospital
    {
        public Hospital()
        {
            HospitalConsultations = new HashSet<HospitalConsultation>();
            HospitalLaboratories = new HashSet<HospitalLaboratory>();
            HospitalLocations = new HashSet<HospitalLocation>();
            HospitalServices = new HashSet<HospitalService>();
            HospitalTags = new HashSet<HospitalTag>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long EntityId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? Remarks { get; set; }
        public string Guid { get; set; } = null!;

        public virtual HospitalEntity Entity { get; set; } = null!;
        public virtual ICollection<HospitalConsultation> HospitalConsultations { get; set; }
        public virtual ICollection<HospitalLaboratory> HospitalLaboratories { get; set; }
        public virtual ICollection<HospitalLocation> HospitalLocations { get; set; }
        public virtual ICollection<HospitalService> HospitalServices { get; set; }
        public virtual ICollection<HospitalTag> HospitalTags { get; set; }
    }
}
