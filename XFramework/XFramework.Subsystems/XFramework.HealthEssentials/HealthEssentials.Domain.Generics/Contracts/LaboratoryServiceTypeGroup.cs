namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryServiceTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<LaboratoryServiceType> LaboratoryServiceTypes { get; } =
        new List<LaboratoryServiceType>();
}