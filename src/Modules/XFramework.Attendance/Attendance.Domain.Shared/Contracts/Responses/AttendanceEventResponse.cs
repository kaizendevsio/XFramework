namespace Attendance.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record AttendanceEventResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SessionId { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid CredentialId { get; set; }
    public AttendanceEventType EventType { get; set; }
    public AttendanceEventSource Source { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid? RecordedByCredentialId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
    public string? Notes { get; set; }
    public string? MetadataJson { get; set; }
    public AttendanceRecordResponse? Record { get; set; }
}

