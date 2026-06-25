namespace Attendance.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetAttendanceContextsRequest : RequestBase,
    IQuery<QueryResponse<GetAttendanceContextsResponse>>,
    IBoltRequest<GetAttendanceContextsRequest, QueryResponse<GetAttendanceContextsResponse>>
{
    public Guid? TenantId { get; set; }
    public AttendanceContextType? ContextType { get; set; }
    public bool? IsActive { get; set; }
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

