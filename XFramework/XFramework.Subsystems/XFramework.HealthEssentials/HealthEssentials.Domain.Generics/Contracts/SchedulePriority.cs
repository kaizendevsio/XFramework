namespace HealthEssentials.Domain.Generics.Contracts;

public partial class SchedulePriority : BaseModel
{
    public Guid TypeId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public short? Status { get; set; }


    public virtual SchedulePriorityType Type { get; set; } = null!;

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}