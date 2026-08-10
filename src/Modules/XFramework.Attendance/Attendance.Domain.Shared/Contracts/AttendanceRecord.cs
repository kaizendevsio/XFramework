namespace Attendance.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/attendance/records",
    RequireAuthorization = true,
    AuthorizationFeature = "attendance",
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "attendance-records"
)]
public partial class AttendanceRecord : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid SessionId { get; set; }

    [MemoryPackOrder(1)]
    public Guid ParticipantId { get; set; }

    [MemoryPackOrder(2)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(3)]
    public DateTime? FirstCheckInAt { get; set; }

    [MemoryPackOrder(4)]
    public DateTime? LastCheckOutAt { get; set; }

    [MemoryPackOrder(5)]
    public AttendanceRecordStatus Status { get; set; } = AttendanceRecordStatus.Unknown;

    [MemoryPackOrder(6)]
    public bool IsManual { get; set; }

    [MemoryPackOrder(7)]
    public Guid? SourceEventId { get; set; }

    [MemoryPackOrder(8)]
    public string? Notes { get; set; }

    [MemoryPackIgnore]
    public virtual AttendanceSession Session { get; set; } = null!;

    [MemoryPackIgnore]
    public virtual AttendanceParticipant Participant { get; set; } = null!;

    [MemoryPackIgnore]
    public virtual ICollection<AttendanceAdjustment> Adjustments { get; set; } = new List<AttendanceAdjustment>();
}

