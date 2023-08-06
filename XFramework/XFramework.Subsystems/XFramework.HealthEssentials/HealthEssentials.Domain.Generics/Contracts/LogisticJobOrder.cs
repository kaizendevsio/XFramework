namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LogisticJobOrder
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid RiderId { get; set; }

    
    public short Status { get; set; }

    public Guid ScheduleId { get; set; }

    public virtual ICollection<LogisticJobOrderDetail> LogisticJobOrderDetails { get; } = new List<LogisticJobOrderDetail>();

    public virtual ICollection<LogisticJobOrderLocation> LogisticJobOrderLocations { get; } = new List<LogisticJobOrderLocation>();

    public virtual LogisticRider? Rider { get; set; }

    public virtual Schedule? Schedule { get; set; }
}
