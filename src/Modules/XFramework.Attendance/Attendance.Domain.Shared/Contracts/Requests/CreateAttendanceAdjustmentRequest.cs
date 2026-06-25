namespace Attendance.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record CreateAttendanceAdjustmentRequest : RequestBase,
    ICommand<QueryResponse<AttendanceAdjustmentResponse>>,
    IBoltRequest<CreateAttendanceAdjustmentRequest, QueryResponse<AttendanceAdjustmentResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid SessionId { get; set; }
    public Guid ParticipantId { get; set; }
    public AttendanceRecordStatus NewStatus { get; set; } = AttendanceRecordStatus.ManualAdjusted;
    public DateTime? AdjustedCheckInAt { get; set; }
    public DateTime? AdjustedCheckOutAt { get; set; }
    public Guid ActorCredentialId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

