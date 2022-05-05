using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class PharmacyJobOrder
    {
        public PharmacyJobOrder()
        {
            PharmacyJobOrderConsultationJobOrders = new HashSet<PharmacyJobOrderConsultationJobOrder>();
            PharmacyJobOrderMedicines = new HashSet<PharmacyJobOrderMedicine>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long PharmacyLocationId { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? Remarks { get; set; }
        public short? Status { get; set; }
        public short? PaymentStatus { get; set; }
        public long? WalletTypeId { get; set; }
        public decimal? AmountDue { get; set; }
        public decimal? AmountPaid { get; set; }
        public decimal? Discount { get; set; }
        public decimal? DiscountType { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? PrescriptionNote { get; set; }
        public string Guid { get; set; } = null!;
        public long ScheduleId { get; set; }

        public virtual PharmacyLocation PharmacyLocation { get; set; } = null!;
        public virtual Schedule Schedule { get; set; } = null!;
        public virtual ICollection<PharmacyJobOrderConsultationJobOrder> PharmacyJobOrderConsultationJobOrders { get; set; }
        public virtual ICollection<PharmacyJobOrderMedicine> PharmacyJobOrderMedicines { get; set; }
    }
}
