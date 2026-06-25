namespace Attendance.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record RecordAttendanceEventRequest : RequestBase,
    ICommand<QueryResponse<AttendanceEventResponse>>,
    IBoltRequest<RecordAttendanceEventRequest, QueryResponse<AttendanceEventResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid SessionId { get; set; }
    public Guid ParticipantId { get; set; }
    public AttendanceEventType EventType { get; set; }
    public AttendanceEventSource Source { get; set; } = AttendanceEventSource.Api;
    public DateTime? OccurredAt { get; set; }
    public Guid? RecordedByCredentialId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
    public string? Notes { get; set; }
    public Dictionary<string, string>? Data { get; set; }
}
