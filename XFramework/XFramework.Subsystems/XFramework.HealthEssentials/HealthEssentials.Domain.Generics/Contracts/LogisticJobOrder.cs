namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LogisticJobOrder : BaseModel
{
    public Guid RiderId { get; set; }


    public short Status { get; set; }

    public Guid ScheduleId { get; set; }

    public virtual ICollection<LogisticJobOrderDetail> LogisticJobOrderDetails { get; } =
        new List<LogisticJobOrderDetail>();

    public virtual ICollection<LogisticJobOrderLocation> LogisticJobOrderLocations { get; } =
        new List<LogisticJobOrderLocation>();

    public virtual LogisticRider? Rider { get; set; }

    public virtual Schedule? Schedule { get; set; }
}