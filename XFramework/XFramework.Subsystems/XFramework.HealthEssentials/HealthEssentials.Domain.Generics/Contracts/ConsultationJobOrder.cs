namespace HealthEssentials.Domain.Generics.Contracts;

public partial class ConsultationJobOrder
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid ConsultationId { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? Remarks { get; set; }

    public short? Status { get; set; }

    public short? PaymentStatus { get; set; }

    public Guid WalletTypeId { get; set; }

    public decimal? AmountDue { get; set; }

    public decimal? AmountPaid { get; set; }

    public decimal? Discount { get; set; }

    public decimal? DiscountType { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? Prescription { get; set; }

    
    public Guid ScheduleId { get; set; }

    public string? Diagnosis { get; set; }

    public string? Treatment { get; set; }

    public string? Symptoms { get; set; }

    public string MeetingLink { get; set; } = null!;

    public virtual Consultation Consultation { get; set; } = null!;

    public virtual ICollection<ConsultationJobOrderLaboratory> ConsultationJobOrderLaboratories { get; } = new List<ConsultationJobOrderLaboratory>();

    public virtual ICollection<ConsultationJobOrderMedicine> ConsultationJobOrderMedicines { get; } = new List<ConsultationJobOrderMedicine>();

    public virtual ICollection<DoctorConsultationJobOrder> DoctorConsultationJobOrders { get; } = new List<DoctorConsultationJobOrder>();

    public virtual ICollection<LaboratoryJobOrder> LaboratoryJobOrders { get; } = new List<LaboratoryJobOrder>();

    public virtual ICollection<PatientAilmentDetail> PatientAilmentDetails { get; } = new List<PatientAilmentDetail>();

    public virtual ICollection<PatientConsultation> PatientConsultations { get; } = new List<PatientConsultation>();

    public virtual ICollection<PatientReminder> PatientReminders { get; } = new List<PatientReminder>();

    public virtual ICollection<PharmacyJobOrderConsultationJobOrder> PharmacyJobOrderConsultationJobOrders { get; } = new List<PharmacyJobOrderConsultationJobOrder>();

    public virtual Schedule Schedule { get; set; } = null!;
}
