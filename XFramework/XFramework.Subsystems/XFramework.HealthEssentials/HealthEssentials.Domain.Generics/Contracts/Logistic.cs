namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Logistic : BaseModel
{
    public Guid TypeId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Remarks { get; set; }


    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? Logo { get; set; }

    public int Status { get; set; }

    public virtual LogisticType Type { get; set; } = null!;

    public virtual ICollection<LogisticRiderHandle> LogisticRiderHandles { get; set; } = new List<LogisticRiderHandle>();
}