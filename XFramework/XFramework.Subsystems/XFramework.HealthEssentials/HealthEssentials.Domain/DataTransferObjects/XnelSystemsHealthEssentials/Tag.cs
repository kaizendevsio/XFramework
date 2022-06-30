using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class Tag
    {
        public Tag()
        {
            AilmentTags = new HashSet<AilmentTag>();
            ConsultationTags = new HashSet<ConsultationTag>();
            DoctorTags = new HashSet<DoctorTag>();
            HospitalServiceTags = new HashSet<HospitalServiceTag>();
            HospitalTags = new HashSet<HospitalTag>();
            LaboratoryLocationTags = new HashSet<LaboratoryLocationTag>();
            LaboratoryServiceTags = new HashSet<LaboratoryServiceTag>();
            LaboratoryTags = new HashSet<LaboratoryTag>();
            LogisticRiderTags = new HashSet<LogisticRiderTag>();
            MedicineTags = new HashSet<MedicineTag>();
            PatientTags = new HashSet<PatientTag>();
            PharmacyServiceTags = new HashSet<PharmacyServiceTag>();
            PharmacyTags = new HashSet<PharmacyTag>();
            ScheduleTags = new HashSet<ScheduleTag>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long EntityId { get; set; }
        public string? Name { get; set; }
        public string? ShortName { get; set; }
        public string? Description { get; set; }
        public string Guid { get; set; } = null!;

        public virtual TagEntity Entity { get; set; } = null!;
        public virtual ICollection<AilmentTag> AilmentTags { get; set; }
        public virtual ICollection<ConsultationTag> ConsultationTags { get; set; }
        public virtual ICollection<DoctorTag> DoctorTags { get; set; }
        public virtual ICollection<HospitalServiceTag> HospitalServiceTags { get; set; }
        public virtual ICollection<HospitalTag> HospitalTags { get; set; }
        public virtual ICollection<LaboratoryLocationTag> LaboratoryLocationTags { get; set; }
        public virtual ICollection<LaboratoryServiceTag> LaboratoryServiceTags { get; set; }
        public virtual ICollection<LaboratoryTag> LaboratoryTags { get; set; }
        public virtual ICollection<LogisticRiderTag> LogisticRiderTags { get; set; }
        public virtual ICollection<MedicineTag> MedicineTags { get; set; }
        public virtual ICollection<PatientTag> PatientTags { get; set; }
        public virtual ICollection<PharmacyServiceTag> PharmacyServiceTags { get; set; }
        public virtual ICollection<PharmacyTag> PharmacyTags { get; set; }
        public virtual ICollection<ScheduleTag> ScheduleTags { get; set; }
    }
}
