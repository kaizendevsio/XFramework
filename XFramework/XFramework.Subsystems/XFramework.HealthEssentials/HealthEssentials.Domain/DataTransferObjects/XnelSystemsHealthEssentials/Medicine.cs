using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class Medicine
    {
        public Medicine()
        {
            ConsultationJobOrderMedicines = new HashSet<ConsultationJobOrderMedicine>();
            MedicineTags = new HashSet<MedicineTag>();
            MedicineVendors = new HashSet<MedicineVendor>();
            PharmacyJobOrderMedicines = new HashSet<PharmacyJobOrderMedicine>();
            PharmacyStocks = new HashSet<PharmacyStock>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long EntityId { get; set; }
        public string? Name { get; set; }
        public string? ShortName { get; set; }
        public string? ScientificName { get; set; }
        public string? Description { get; set; }
        public string? ChemicalComponent { get; set; }
        public string Guid { get; set; } = null!;

        public virtual MedicineEntity Entity { get; set; } = null!;
        public virtual ICollection<ConsultationJobOrderMedicine> ConsultationJobOrderMedicines { get; set; }
        public virtual ICollection<MedicineTag> MedicineTags { get; set; }
        public virtual ICollection<MedicineVendor> MedicineVendors { get; set; }
        public virtual ICollection<PharmacyJobOrderMedicine> PharmacyJobOrderMedicines { get; set; }
        public virtual ICollection<PharmacyStock> PharmacyStocks { get; set; }
    }
}
