namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LogisticRiderHandle : BaseModel
{
    public Guid LogisticId { get; set; }

    public Guid LogisticRiderId { get; set; }

    public short Status { get; set; }


    public virtual Logistic Logistic { get; set; } = null!;

    public virtual LogisticRider LogisticRider { get; set; } = null!;
}