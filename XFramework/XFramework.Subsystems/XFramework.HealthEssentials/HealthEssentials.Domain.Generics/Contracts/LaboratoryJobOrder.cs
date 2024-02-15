namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryJobOrder : BaseModel
{
    public Guid LaboratoryLocationId { get; set; }

    public Guid LaboratoryId { get; set; }

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


    public Guid ScheduleId { get; set; }

    public Guid ConsultationJobOrderId { get; set; }

    public Guid PatientId { get; set; }

    public virtual ConsultationJobOrder? ConsultationJobOrder { get; set; }

    public virtual Laboratory Laboratory { get; set; } = null!;

    public virtual ICollection<LaboratoryJobOrderDetail> LaboratoryJobOrderDetails { get; set; } =
        new List<LaboratoryJobOrderDetail>();

    public virtual ICollection<LaboratoryJobOrderResult> LaboratoryJobOrderResults { get; set; } =
        new List<LaboratoryJobOrderResult>();

    public virtual LaboratoryLocation LaboratoryLocation { get; set; } = null!;

    public virtual Patient? Patient { get; set; }

    public virtual ICollection<PatientLaboratory> PatientLaboratories { get; set; } = new List<PatientLaboratory>();

    public virtual Schedule Schedule { get; set; } = null!;
}