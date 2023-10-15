namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PharmacyServiceTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<PharmacyServiceType> PharmacyServiceTypes { get; } = new List<PharmacyServiceType>();
}