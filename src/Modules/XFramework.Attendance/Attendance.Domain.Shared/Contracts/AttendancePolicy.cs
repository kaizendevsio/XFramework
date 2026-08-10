namespace Attendance.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/attendance/policies",
    RequireAuthorization = true,
    AuthorizationFeature = "attendance",
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "attendance-policies"
)]
public partial class AttendancePolicy : BaseModel
{
    [MemoryPackOrder(0)]
    public string Name { get; set; } = string.Empty;

    [MemoryPackOrder(1)]
    public string? Description { get; set; }

    [MemoryPackOrder(2)]
    public int GracePeriodMinutes { get; set; } = 5;

    [MemoryPackOrder(3)]
    public int EarlyCheckoutGraceMinutes { get; set; } = 0;

    [MemoryPackOrder(4)]
    public bool CheckoutRequired { get; set; } = true;

    [MemoryPackOrder(5)]
    public string TimeZoneId { get; set; } = "UTC";

    [MemoryPackIgnore]
    public virtual ICollection<AttendanceContext> Contexts { get; set; } = new List<AttendanceContext>();

    [MemoryPackIgnore]
    public virtual ICollection<AttendanceSession> Sessions { get; set; } = new List<AttendanceSession>();
}

