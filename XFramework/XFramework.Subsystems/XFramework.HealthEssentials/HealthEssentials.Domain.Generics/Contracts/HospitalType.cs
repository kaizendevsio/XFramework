namespace HealthEssentials.Domain.Generics.Contracts;

public partial class HospitalType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual HospitalTypeGroup Group { get; set; } = null!;

    public virtual ICollection<Hospital> Hospitals { get; set; } = new List<Hospital>();
}