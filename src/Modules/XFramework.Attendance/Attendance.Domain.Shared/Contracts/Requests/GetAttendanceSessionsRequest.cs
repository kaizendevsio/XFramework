namespace Attendance.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetAttendanceSessionsRequest : RequestBase,
    IQuery<QueryResponse<GetAttendanceSessionsResponse>>,
    IBoltRequest<GetAttendanceSessionsRequest, QueryResponse<GetAttendanceSessionsResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid ContextId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public AttendanceSessionStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

