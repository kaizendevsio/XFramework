namespace HealthEssentials.Domain.Generics.Contracts;

public partial class SchedulePriorityType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public int? SortOrder { get; set; }

    public virtual ICollection<SchedulePriority> SchedulePriorities { get; set; } = new List<SchedulePriority>();
}