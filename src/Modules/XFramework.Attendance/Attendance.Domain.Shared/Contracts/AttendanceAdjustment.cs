namespace Attendance.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/attendance/adjustments",
    RequireAuthorization = true,
    CacheDurationSeconds = 0,
    CacheKeyPrefix = "attendance-adjustments"
)]
public partial class AttendanceAdjustment : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid? RecordId { get; set; }

    [MemoryPackOrder(1)]
    public Guid SessionId { get; set; }

    [MemoryPackOrder(2)]
    public Guid ParticipantId { get; set; }

    [MemoryPackOrder(3)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(4)]
    public AttendanceRecordStatus PreviousStatus { get; set; }

    [MemoryPackOrder(5)]
    public AttendanceRecordStatus NewStatus { get; set; }

    [MemoryPackOrder(6)]
    public DateTime? AdjustedCheckInAt { get; set; }

    [MemoryPackOrder(7)]
    public DateTime? AdjustedCheckOutAt { get; set; }

    [MemoryPackOrder(8)]
    public Guid ActorCredentialId { get; set; }

    [MemoryPackOrder(9)]
    public string Reason { get; set; } = string.Empty;

    [MemoryPackOrder(10)]
    public string? Notes { get; set; }

    [MemoryPackIgnore]
    public virtual AttendanceRecord? Record { get; set; }
}

