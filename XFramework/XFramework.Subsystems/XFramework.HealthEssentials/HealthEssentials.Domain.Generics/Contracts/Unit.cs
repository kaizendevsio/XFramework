namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Unit : BaseModel
{
    public Guid TypeId { get; set; }

    public string? Name { get; set; }

    public string? ShortName { get; set; }

    public string? Description { get; set; }


    public virtual ICollection<ConsultationJobOrderMedicine> ConsultationJobOrderMedicineDosageUnits { get; set; } =
        new List<ConsultationJobOrderMedicine>();

    public virtual ICollection<ConsultationJobOrderMedicine> ConsultationJobOrderMedicineDurationUnits { get; set; } =
        new List<ConsultationJobOrderMedicine>();

    public virtual ICollection<ConsultationJobOrderMedicine> ConsultationJobOrderMedicineIntakeUnits { get; set; } =
        new List<ConsultationJobOrderMedicine>();

    public virtual UnitType Type { get; set; } = null!;

    public virtual ICollection<HospitalService> HospitalServices { get; set; } = new List<HospitalService>();

    public virtual ICollection<LaboratoryService> LaboratoryServices { get; set; } = new List<LaboratoryService>();

    public virtual ICollection<LogisticJobOrderDetail> LogisticJobOrderDetails { get; set; } =
        new List<LogisticJobOrderDetail>();

    public virtual ICollection<MedicineIntake> MedicineIntakes { get; set; } = new List<MedicineIntake>();

    public virtual ICollection<PharmacyJobOrderMedicine> PharmacyJobOrderMedicineDosageUnits { get; set; } =
        new List<PharmacyJobOrderMedicine>();

    public virtual ICollection<PharmacyJobOrderMedicine> PharmacyJobOrderMedicineDurationUnits { get; set; } =
        new List<PharmacyJobOrderMedicine>();

    public virtual ICollection<PharmacyJobOrderMedicine> PharmacyJobOrderMedicineIntakeUnits { get; set; } =
        new List<PharmacyJobOrderMedicine>();

    public virtual ICollection<PharmacyService> PharmacyServices { get; set; } = new List<PharmacyService>();

    public virtual ICollection<PharmacyStock> PharmacyStocks { get; set; } = new List<PharmacyStock>();
}