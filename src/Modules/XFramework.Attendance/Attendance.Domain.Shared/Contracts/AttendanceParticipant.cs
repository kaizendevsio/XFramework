namespace Attendance.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/attendance/participants",
    RequireAuthorization = true,
    AuthorizationFeature = "attendance",
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "attendance-participants"
)]
public partial class AttendanceParticipant : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid ContextId { get; set; }

    [MemoryPackOrder(1)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(2)]
    public string? DisplayName { get; set; }

    [MemoryPackOrder(3)]
    public string? ReferenceCode { get; set; }

    [MemoryPackOrder(4)]
    public DateTime StartedAt { get; set; }

    [MemoryPackOrder(5)]
    public DateTime? EndedAt { get; set; }

    [MemoryPackOrder(6)]
    public bool IsActive { get; set; } = true;

    [MemoryPackIgnore]
    public virtual AttendanceContext Context { get; set; } = null!;

    [MemoryPackIgnore]
    public virtual ICollection<AttendanceRecord> Records { get; set; } = new List<AttendanceRecord>();
}

