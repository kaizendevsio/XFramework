namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Medicine : BaseModel
{
    public Guid TypeId { get; set; }

    public string? Name { get; set; }

    public string? ShortName { get; set; }

    public string? ScientificName { get; set; }

    public string? Description { get; set; }

    public string? ChemicalComponent { get; set; }


    public virtual ICollection<ConsultationJobOrderMedicine> ConsultationJobOrderMedicines { get; } =
        new List<ConsultationJobOrderMedicine>();

    public virtual MedicineType Type { get; set; } = null!;

    public virtual ICollection<MedicineTag> MedicineTags { get; } = new List<MedicineTag>();

    public virtual ICollection<MedicineVendor> MedicineVendors { get; } = new List<MedicineVendor>();

    public virtual ICollection<PharmacyJobOrderMedicine> PharmacyJobOrderMedicines { get; } =
        new List<PharmacyJobOrderMedicine>();

    public virtual ICollection<PharmacyStock> PharmacyStocks { get; } = new List<PharmacyStock>();
}