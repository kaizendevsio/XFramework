namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual LaboratoryTypeGroup Group { get; set; } = null!;

    public virtual ICollection<Laboratory> Laboratories { get; } = new List<Laboratory>();
}