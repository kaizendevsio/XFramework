

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Schedule;

public class ScheduleResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public short? Status { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime ExpireAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? Guid { get; set; }

    public ScheduleEntityResponse? Entity { get; set; }
    public SchedulePriorityResponse? Priority { get; set; }
}