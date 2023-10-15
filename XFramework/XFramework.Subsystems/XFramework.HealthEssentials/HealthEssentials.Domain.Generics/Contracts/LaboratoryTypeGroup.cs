namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<LaboratoryType> LaboratoryTypes { get; } = new List<LaboratoryType>();
}