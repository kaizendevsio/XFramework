namespace HealthEssentials.Domain.Generics.Contracts;

public partial class ScheduleTag : BaseModel
{
    public Guid ScheduleId { get; set; }

    public string? Value { get; set; }


    public Guid TagId { get; set; }

    public virtual Schedule Schedule { get; set; } = null!;

    public virtual Tag Tag { get; set; } = null!;
}