using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class Doctor
    {
        public Doctor()
        {
            DoctorConsultationJobOrders = new HashSet<DoctorConsultationJobOrder>();
            DoctorConsultations = new HashSet<DoctorConsultation>();
            DoctorTags = new HashSet<DoctorTag>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long EntityId { get; set; }
        public long CredentialId { get; set; }
        public string? Description { get; set; }
        public string? Remarks { get; set; }
        public string Guid { get; set; } = null!;

        public virtual DoctorEntity Entity { get; set; } = null!;
        public virtual ICollection<DoctorConsultationJobOrder> DoctorConsultationJobOrders { get; set; }
        public virtual ICollection<DoctorConsultation> DoctorConsultations { get; set; }
        public virtual ICollection<DoctorTag> DoctorTags { get; set; }
    }
}
