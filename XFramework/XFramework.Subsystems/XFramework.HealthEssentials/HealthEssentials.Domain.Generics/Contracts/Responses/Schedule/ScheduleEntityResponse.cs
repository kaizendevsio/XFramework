

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Schedule;

public class ScheduleEntityResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Guid { get; set; } = null!;
    public long GroupId { get; set; }
    public int? SortOrder { get; set; }

    public ScheduleEntityGroupResponse Group { get; set; } = null!;
}