namespace Attendance.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetAttendanceRecordRequest : RequestBase,
    IQuery<QueryResponse<AttendanceRecordResponse>>,
    IBoltRequest<GetAttendanceRecordRequest, QueryResponse<AttendanceRecordResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid SessionId { get; set; }
    public Guid ParticipantId { get; set; }
}

