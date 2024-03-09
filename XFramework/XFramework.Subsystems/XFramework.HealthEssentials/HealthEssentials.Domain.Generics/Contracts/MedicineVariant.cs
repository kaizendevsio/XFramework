namespace HealthEssentials.Domain.Generics.Contracts;

public partial class MedicineVariant : BaseModel
{
    public Guid MedicineId { get; set; }

    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal Dosage { get; set; }
    public Guid? UnitId { get; set; }
    public Unit? Unit { get; set; }
    
    public virtual Medicine? Medicine { get; set; }
    public virtual ICollection<PharmacyStock> PharmacyStocks { get; set; } = new List<PharmacyStock>();
    public virtual ICollection<MedicineTag> MedicineTags { get; set; } = new List<MedicineTag>();
    public virtual ICollection<PharmacyJobOrderMedicine> PharmacyJobOrderMedicines { get; set; } = new List<PharmacyJobOrderMedicine>();
    public virtual ICollection<ConsultationJobOrderMedicine> ConsultationJobOrderMedicines { get; set; } = new List<ConsultationJobOrderMedicine>();
}