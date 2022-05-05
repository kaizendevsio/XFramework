using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class LaboratoryJobOrder
    {
        public LaboratoryJobOrder()
        {
            LaboratoryJobOrderDetails = new HashSet<LaboratoryJobOrderDetail>();
            LaboratoryJobOrderResults = new HashSet<LaboratoryJobOrderResult>();
            PatientLaboratories = new HashSet<PatientLaboratory>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long LaboratoryLocationId { get; set; }
        public long LaboratoryId { get; set; }
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
        public string Guid { get; set; } = null!;
        public long ScheduleId { get; set; }

        public virtual Laboratory Laboratory { get; set; } = null!;
        public virtual LaboratoryLocation LaboratoryLocation { get; set; } = null!;
        public virtual Schedule Schedule { get; set; } = null!;
        public virtual ICollection<LaboratoryJobOrderDetail> LaboratoryJobOrderDetails { get; set; }
        public virtual ICollection<LaboratoryJobOrderResult> LaboratoryJobOrderResults { get; set; }
        public virtual ICollection<PatientLaboratory> PatientLaboratories { get; set; }
    }
}
