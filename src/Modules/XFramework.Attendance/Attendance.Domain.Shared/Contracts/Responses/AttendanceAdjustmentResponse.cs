namespace Attendance.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record AttendanceAdjustmentResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? RecordId { get; set; }
    public Guid SessionId { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid CredentialId { get; set; }
    public AttendanceRecordStatus PreviousStatus { get; set; }
    public AttendanceRecordStatus NewStatus { get; set; }
    public DateTime? AdjustedCheckInAt { get; set; }
    public DateTime? AdjustedCheckOutAt { get; set; }
    public Guid ActorCredentialId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public AttendanceRecordResponse? Record { get; set; }
}

