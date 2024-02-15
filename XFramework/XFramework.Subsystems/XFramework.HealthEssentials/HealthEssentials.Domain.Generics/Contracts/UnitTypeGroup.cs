namespace HealthEssentials.Domain.Generics.Contracts;

public partial class UnitTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<UnitType> UnitTypes { get; set; } = new List<UnitType>();
}