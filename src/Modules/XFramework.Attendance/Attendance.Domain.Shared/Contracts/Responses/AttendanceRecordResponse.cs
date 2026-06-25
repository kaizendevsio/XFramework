namespace Attendance.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record AttendanceRecordResponse
{
    public Guid? Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SessionId { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid CredentialId { get; set; }
    public DateTime? FirstCheckInAt { get; set; }
    public DateTime? LastCheckOutAt { get; set; }
    public AttendanceRecordStatus Status { get; set; }
    public bool IsManual { get; set; }
    public Guid? SourceEventId { get; set; }
    public string? Notes { get; set; }
}

