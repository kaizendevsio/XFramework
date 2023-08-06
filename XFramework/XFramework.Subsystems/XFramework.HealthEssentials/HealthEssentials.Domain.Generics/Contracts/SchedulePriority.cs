namespace HealthEssentials.Domain.Generics.Contracts;

public partial class SchedulePriority
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid TypeId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public short? Status { get; set; }

    
    public virtual SchedulePriorityType Type { get; set; } = null!;

    public virtual ICollection<Schedule> Schedules { get; } = new List<Schedule>();
}
