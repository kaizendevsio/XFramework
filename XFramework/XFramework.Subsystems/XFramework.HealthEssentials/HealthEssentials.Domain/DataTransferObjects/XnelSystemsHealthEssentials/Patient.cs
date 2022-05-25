using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class Patient
    {
        public Patient()
        {
            LaboratoryJobOrders = new HashSet<LaboratoryJobOrder>();
            PatientAilments = new HashSet<PatientAilment>();
            PatientConsultations = new HashSet<PatientConsultation>();
            PatientLaboratories = new HashSet<PatientLaboratory>();
            PatientReminders = new HashSet<PatientReminder>();
            PatientTags = new HashSet<PatientTag>();
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

        public virtual PatientEntity Entity { get; set; } = null!;
        public virtual ICollection<LaboratoryJobOrder> LaboratoryJobOrders { get; set; }
        public virtual ICollection<PatientAilment> PatientAilments { get; set; }
        public virtual ICollection<PatientConsultation> PatientConsultations { get; set; }
        public virtual ICollection<PatientLaboratory> PatientLaboratories { get; set; }
        public virtual ICollection<PatientReminder> PatientReminders { get; set; }
        public virtual ICollection<PatientTag> PatientTags { get; set; }
    }
}
