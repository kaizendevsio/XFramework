using System.ComponentModel.DataAnnotations.Schema;

namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PharmacyJobOrderMedicine : BaseModel
{
    public Guid PharmacyJobOrderId { get; set; }

    public Guid MedicineVariantId { get; set; }
    
    public int Quantity { get; set; }

    public string? PrescriptionNote { get; set; }

    public string? Remarks { get; set; }

    public short? Status { get; set; }

    public Guid UnavailabilityHandling { get; set; }

    public int IntakeRepetition { get; set; }

    public Guid IntakeUnitId { get; set; }

    public decimal Duration { get; set; }

    public Guid DurationUnitId { get; set; }

    public int? Dosage { get; set; }

    public Guid DosageUnitId { get; set; }

    public virtual Unit? DosageUnit { get; set; }

    public virtual Unit DurationUnit { get; set; } = null!;

    public virtual Unit IntakeUnit { get; set; } = null!;

    public virtual MedicineVariant? MedicineVariant { get; set; }
    
    public virtual PharmacyJobOrder PharmacyJobOrder { get; set; } = null!;

    [NotMapped]
    public bool? IsAvailable { get; set; }
}