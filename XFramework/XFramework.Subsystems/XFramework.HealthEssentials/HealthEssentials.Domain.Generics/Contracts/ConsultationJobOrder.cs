namespace HealthEssentials.Domain.Generics.Contracts;

public partial class ConsultationJobOrder : BaseModel
{
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

    public virtual ICollection<ConsultationJobOrderLaboratory> ConsultationJobOrderLaboratories { get; set; } =
        new List<ConsultationJobOrderLaboratory>();

    public virtual ICollection<ConsultationJobOrderMedicine> ConsultationJobOrderMedicines { get; set; } =
        new List<ConsultationJobOrderMedicine>();

    public virtual ICollection<DoctorConsultationJobOrder> DoctorConsultationJobOrders { get; set; } =
        new List<DoctorConsultationJobOrder>();

    public virtual ICollection<LaboratoryJobOrder> LaboratoryJobOrders { get; set; } = new List<LaboratoryJobOrder>();

    public virtual ICollection<PatientAilmentDetail> PatientAilmentDetails { get; set; } = new List<PatientAilmentDetail>();

    public virtual ICollection<PatientConsultation> PatientConsultations { get; set; } = new List<PatientConsultation>();

    public virtual ICollection<PatientReminder> PatientReminders { get; set; } = new List<PatientReminder>();

    public virtual ICollection<PharmacyJobOrderConsultationJobOrder> PharmacyJobOrderConsultationJobOrders { get; set; } =
        new List<PharmacyJobOrderConsultationJobOrder>();

    public virtual Schedule Schedule { get; set; } = null!;
}