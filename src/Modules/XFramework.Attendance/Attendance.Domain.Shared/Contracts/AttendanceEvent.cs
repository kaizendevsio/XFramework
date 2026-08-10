namespace Attendance.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/attendance/events",
    RequireAuthorization = true,
    AuthorizationFeature = "attendance",
    CacheDurationSeconds = 0,
    CacheKeyPrefix = "attendance-events"
)]
public partial class AttendanceEvent : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid SessionId { get; set; }

    [MemoryPackOrder(1)]
    public Guid ParticipantId { get; set; }

    [MemoryPackOrder(2)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(3)]
    public AttendanceEventType EventType { get; set; }

    [MemoryPackOrder(4)]
    public AttendanceEventSource Source { get; set; }

    [MemoryPackOrder(5)]
    public DateTime OccurredAt { get; set; }

    [MemoryPackOrder(6)]
    public Guid? RecordedByCredentialId { get; set; }

    [MemoryPackOrder(7)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [MemoryPackOrder(8)]
    public string? SourceReference { get; set; }

    [MemoryPackOrder(9)]
    public string? Notes { get; set; }

    [MemoryPackOrder(10)]
    public string? MetadataJson { get; set; }

    [MemoryPackIgnore]
    public virtual AttendanceSession Session { get; set; } = null!;

    [MemoryPackIgnore]
    public virtual AttendanceParticipant Participant { get; set; } = null!;
}

