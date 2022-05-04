using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class MedicineIntake
    {
        public MedicineIntake()
        {
            ConsultationJobOrderMedicines = new HashSet<ConsultationJobOrderMedicine>();
            PharmacyJobOrderMedicines = new HashSet<PharmacyJobOrderMedicine>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long EntityId { get; set; }
        public string? Name { get; set; }
        public string? ScientificName { get; set; }
        public string? Description { get; set; }
        public long? Repetition { get; set; }
        public long? UnitId { get; set; }
        public string Guid { get; set; } = null!;

        public virtual MedicineIntakeEntity Entity { get; set; } = null!;
        public virtual Unit? Unit { get; set; }
        public virtual ICollection<ConsultationJobOrderMedicine> ConsultationJobOrderMedicines { get; set; }
        public virtual ICollection<PharmacyJobOrderMedicine> PharmacyJobOrderMedicines { get; set; }
    }
}
