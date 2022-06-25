using HealthEssentials.Domain.Generics.Contracts.Responses.Tag;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Schedule;

public class ScheduleTagResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Value { get; set; }
    public string Guid { get; set; } = null!;

    public ScheduleResponse? Schedule { get; set; }
    public TagResponse? Tag { get; set; }
}