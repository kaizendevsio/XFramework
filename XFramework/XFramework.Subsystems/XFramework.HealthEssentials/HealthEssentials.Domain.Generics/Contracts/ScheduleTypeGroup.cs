namespace HealthEssentials.Domain.Generics.Contracts;

public partial class ScheduleTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<ScheduleType> ScheduleTypes { get; } = new List<ScheduleType>();
}