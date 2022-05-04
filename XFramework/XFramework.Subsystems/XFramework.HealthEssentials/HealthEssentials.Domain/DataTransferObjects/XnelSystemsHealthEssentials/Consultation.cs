using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class Consultation
    {
        public Consultation()
        {
            ConsultationJobOrders = new HashSet<ConsultationJobOrder>();
            ConsultationTags = new HashSet<ConsultationTag>();
            DoctorConsultations = new HashSet<DoctorConsultation>();
            HospitalConsultations = new HashSet<HospitalConsultation>();
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

        public virtual ConsultationEntity Entity { get; set; } = null!;
        public virtual ICollection<ConsultationJobOrder> ConsultationJobOrders { get; set; }
        public virtual ICollection<ConsultationTag> ConsultationTags { get; set; }
        public virtual ICollection<DoctorConsultation> DoctorConsultations { get; set; }
        public virtual ICollection<HospitalConsultation> HospitalConsultations { get; set; }
    }
}
