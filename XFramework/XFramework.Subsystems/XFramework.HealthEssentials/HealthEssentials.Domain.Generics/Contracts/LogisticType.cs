namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LogisticType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public int? SortOrder { get; set; }

    public virtual ICollection<Logistic> Logistics { get; } = new List<Logistic>();
}