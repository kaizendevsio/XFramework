namespace Attendance.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetAttendanceReportRequest : RequestBase,
    IQuery<QueryResponse<AttendanceReportResponse>>,
    IBoltRequest<GetAttendanceReportRequest, QueryResponse<AttendanceReportResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid ContextId { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

