namespace Attendance.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/attendance/sessions",
    RequireAuthorization = true,
    AuthorizationFeature = "attendance",
    CacheDurationSeconds = 120,
    CacheKeyPrefix = "attendance-sessions"
)]
public partial class AttendanceSession : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid ContextId { get; set; }

    [MemoryPackOrder(1)]
    public Guid? PolicyId { get; set; }

    [MemoryPackOrder(2)]
    public string Name { get; set; } = string.Empty;

    [MemoryPackOrder(3)]
    public string? Code { get; set; }

    [MemoryPackOrder(4)]
    public DateTime StartsAt { get; set; }

    [MemoryPackOrder(5)]
    public DateTime EndsAt { get; set; }

    [MemoryPackOrder(6)]
    public string TimeZoneId { get; set; } = "UTC";

    [MemoryPackOrder(7)]
    public AttendanceSessionStatus Status { get; set; } = AttendanceSessionStatus.Scheduled;

    [MemoryPackIgnore]
    public virtual AttendanceContext Context { get; set; } = null!;

    [MemoryPackIgnore]
    public virtual AttendancePolicy? Policy { get; set; }

    [MemoryPackIgnore]
    public virtual ICollection<AttendanceEvent> Events { get; set; } = new List<AttendanceEvent>();

    [MemoryPackIgnore]
    public virtual ICollection<AttendanceRecord> Records { get; set; } = new List<AttendanceRecord>();
}

