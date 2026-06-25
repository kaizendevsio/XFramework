namespace Attendance.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record AttendanceReportResponse
{
    public Guid TenantId { get; set; }
    public Guid ContextId { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int ActiveParticipantCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalSessions { get; set; }
    public int TotalPages { get; set; }
    public List<AttendanceSessionReportItemResponse> Sessions { get; set; } = [];
}

