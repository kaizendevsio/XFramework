using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class PharmacyJobOrderMedicine
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long PharmacyJobOrderId { get; set; }
        public long? MedicineId { get; set; }
        public long? MedicineIntakeId { get; set; }
        public int Quantity { get; set; }
        public string? PrescriptionNote { get; set; }
        public string? Remarks { get; set; }
        public short? Status { get; set; }
        public string Guid { get; set; } = null!;

        public virtual Medicine? Medicine { get; set; }
        public virtual MedicineIntake? MedicineIntake { get; set; }
        public virtual PharmacyJobOrder PharmacyJobOrder { get; set; } = null!;
    }
}
