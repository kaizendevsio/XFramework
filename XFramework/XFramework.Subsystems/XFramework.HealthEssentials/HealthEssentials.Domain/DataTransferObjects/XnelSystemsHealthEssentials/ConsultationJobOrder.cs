using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class ConsultationJobOrder
    {
        public ConsultationJobOrder()
        {
            ConsultationJobOrderLaboratories = new HashSet<ConsultationJobOrderLaboratory>();
            ConsultationJobOrderMedicines = new HashSet<ConsultationJobOrderMedicine>();
            DoctorConsultationJobOrders = new HashSet<DoctorConsultationJobOrder>();
            PatientAilmentDetails = new HashSet<PatientAilmentDetail>();
            PatientConsultations = new HashSet<PatientConsultation>();
            PatientReminders = new HashSet<PatientReminder>();
            PharmacyJobOrderConsultationJobOrders = new HashSet<PharmacyJobOrderConsultationJobOrder>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long ConsultationId { get; set; }
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

        public virtual Consultation Consultation { get; set; } = null!;
        public virtual ICollection<ConsultationJobOrderLaboratory> ConsultationJobOrderLaboratories { get; set; }
        public virtual ICollection<ConsultationJobOrderMedicine> ConsultationJobOrderMedicines { get; set; }
        public virtual ICollection<DoctorConsultationJobOrder> DoctorConsultationJobOrders { get; set; }
        public virtual ICollection<PatientAilmentDetail> PatientAilmentDetails { get; set; }
        public virtual ICollection<PatientConsultation> PatientConsultations { get; set; }
        public virtual ICollection<PatientReminder> PatientReminders { get; set; }
        public virtual ICollection<PharmacyJobOrderConsultationJobOrder> PharmacyJobOrderConsultationJobOrders { get; set; }
    }
}
