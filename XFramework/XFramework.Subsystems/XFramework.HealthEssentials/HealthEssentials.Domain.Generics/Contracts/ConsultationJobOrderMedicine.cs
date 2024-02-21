namespace HealthEssentials.Domain.Generics.Contracts;

public partial class ConsultationJobOrderMedicine : BaseModel
{
    public Guid ConsultationJobOrderId { get; set; }

    public Guid MedicineId { get; set; }
    
    public int Quantity { get; set; }

    public string? PrescriptionNote { get; set; }

    public string? Remarks { get; set; }

    public short? Status { get; set; }


    public int IntakeRepetition { get; set; }

    public Guid IntakeUnitId { get; set; }

    public decimal Duration { get; set; }

    public Guid DurationUnitId { get; set; }

    public int? Dosage { get; set; }

    public Guid DosageUnitId { get; set; }

    public virtual ConsultationJobOrder ConsultationJobOrder { get; set; } = null!;

    public virtual Unit? DosageUnit { get; set; }

    public virtual Unit DurationUnit { get; set; } = null!;

    public virtual Unit IntakeUnit { get; set; } = null!;

    public virtual Medicine? Medicine { get; set; }

}