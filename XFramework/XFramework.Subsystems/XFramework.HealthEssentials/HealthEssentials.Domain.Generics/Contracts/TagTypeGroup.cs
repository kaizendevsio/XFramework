namespace HealthEssentials.Domain.Generics.Contracts;

public partial class TagTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<TagType> TagTypes { get; } = new List<TagType>();
}