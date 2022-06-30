using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class PatientAilment
    {
        public PatientAilment()
        {
            PatientAilmentDetails = new HashSet<PatientAilmentDetail>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long PatientId { get; set; }
        public long AilmentId { get; set; }
        public string? Remarks { get; set; }
        public string Guid { get; set; } = null!;

        public virtual Ailment Ailment { get; set; } = null!;
        public virtual Patient Patient { get; set; } = null!;
        public virtual ICollection<PatientAilmentDetail> PatientAilmentDetails { get; set; }
    }
}
