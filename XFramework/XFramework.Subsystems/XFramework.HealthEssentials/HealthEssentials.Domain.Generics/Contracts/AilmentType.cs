namespace HealthEssentials.Domain.Generics.Contracts;

public partial class AilmentType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual ICollection<Ailment> Ailments { get; } = new List<Ailment>();

    public virtual AilmentTypeGroup Group { get; set; } = null!;
}