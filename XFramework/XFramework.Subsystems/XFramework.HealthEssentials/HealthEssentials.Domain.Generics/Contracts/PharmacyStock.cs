namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PharmacyStock : BaseModel
{
    public Guid PharmacyId { get; set; }

    public Guid MedicineId { get; set; }

    public DateTime LastRestock { get; set; }

    public Guid AvailableQuantity { get; set; }

    public Guid CriticalQuantity { get; set; }

    public Guid MinQuantity { get; set; }

    public Guid MaxQuantity { get; set; }

    public Guid Unit { get; set; }


    public virtual Medicine Medicine { get; set; } = null!;

    public virtual Pharmacy Pharmacy { get; set; } = null!;

    public virtual Unit? UnitNavigation { get; set; }
}