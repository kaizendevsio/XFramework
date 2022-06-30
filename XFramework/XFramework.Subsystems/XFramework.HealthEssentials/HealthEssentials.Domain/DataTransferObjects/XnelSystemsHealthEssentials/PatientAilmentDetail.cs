using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class PatientAilmentDetail
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long PatientAilmentId { get; set; }
        public long? ConsultationJobOrderId { get; set; }
        public string? DoctorName { get; set; }
        public short? Status { get; set; }
        public string? Remarks { get; set; }
        public string Guid { get; set; } = null!;

        public virtual ConsultationJobOrder? ConsultationJobOrder { get; set; }
        public virtual PatientAilment PatientAilment { get; set; } = null!;
    }
}
