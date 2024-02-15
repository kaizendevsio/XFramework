namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LogisticRiderTag : BaseModel
{
    public Guid LogisticRiderId { get; set; }

    public string? Value { get; set; }

    public Guid TagId { get; set; }


    public virtual LogisticRider LogisticRider { get; set; } = null!;

    public virtual Tag? Tag { get; set; }
}