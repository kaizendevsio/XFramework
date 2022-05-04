using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class PatientLaboratory
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long PatientId { get; set; }
        public long? LaboratoryJobOrderId { get; set; }
        public string Guid { get; set; } = null!;

        public virtual LaboratoryJobOrder? LaboratoryJobOrder { get; set; }
        public virtual Patient Patient { get; set; } = null!;
    }
}
