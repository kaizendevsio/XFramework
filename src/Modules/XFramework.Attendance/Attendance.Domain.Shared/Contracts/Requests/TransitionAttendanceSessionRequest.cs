namespace Attendance.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record TransitionAttendanceSessionRequest : RequestBase,
    ICommand<QueryResponse<AttendanceSessionResponse>>,
    IBoltRequest<TransitionAttendanceSessionRequest, QueryResponse<AttendanceSessionResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid SessionId { get; set; }
    public AttendanceSessionStatus Status { get; set; }
}
