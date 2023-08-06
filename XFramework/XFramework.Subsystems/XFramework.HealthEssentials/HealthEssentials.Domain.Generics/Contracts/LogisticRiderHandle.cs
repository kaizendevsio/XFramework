namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LogisticRiderHandle
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid LogisticId { get; set; }

    public Guid LogisticRiderId { get; set; }

    public short Status { get; set; }

    
    public virtual Logistic Logistic { get; set; } = null!;

    public virtual LogisticRider LogisticRider { get; set; } = null!;
}
