namespace HealthEssentials.Domain.Generics.Contracts;

public partial class AilmentTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<AilmentType> AilmentTypes { get; } = new List<AilmentType>();
}