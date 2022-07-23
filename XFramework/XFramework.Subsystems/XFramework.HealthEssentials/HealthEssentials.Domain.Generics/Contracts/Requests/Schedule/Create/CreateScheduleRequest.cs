using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Schedule.Create;

public class CreateScheduleRequest : RequestBase
{
    public Guid? EntityGuid { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public short? Status { get; set; }
    public Guid? PriorityGuid { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime ExpireAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}