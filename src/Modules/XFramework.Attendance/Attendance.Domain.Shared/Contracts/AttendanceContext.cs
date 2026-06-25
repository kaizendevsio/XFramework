namespace Attendance.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/attendance/contexts",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "attendance-contexts"
)]
public partial class AttendanceContext : BaseModel
{
    [MemoryPackOrder(0)]
    public string Name { get; set; } = string.Empty;

    [MemoryPackOrder(1)]
    public string? Code { get; set; }

    [MemoryPackOrder(2)]
    public AttendanceContextType ContextType { get; set; }

    [MemoryPackOrder(3)]
    public string? Description { get; set; }

    [MemoryPackOrder(4)]
    public Guid? DefaultPolicyId { get; set; }

    [MemoryPackOrder(5)]
    public bool IsActive { get; set; } = true;

    [MemoryPackIgnore]
    public virtual AttendancePolicy? DefaultPolicy { get; set; }

    [MemoryPackIgnore]
    public virtual ICollection<AttendanceParticipant> Participants { get; set; } = new List<AttendanceParticipant>();

    [MemoryPackIgnore]
    public virtual ICollection<AttendanceSession> Sessions { get; set; } = new List<AttendanceSession>();
}

