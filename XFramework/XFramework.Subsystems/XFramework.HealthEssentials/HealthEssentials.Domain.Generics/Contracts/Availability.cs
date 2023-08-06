namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Availability
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

    public DateTime DateStart { get; set; }

    public DateTime DateEnd { get; set; }

    public DateTime TimeStart { get; set; }

    public DateTime TimeEnd { get; set; }

    public bool? IsAvailable { get; set; }

    
    public virtual ScheduleType Type { get; set; } = null!;
}
