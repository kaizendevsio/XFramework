namespace Attendance.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record CreateAttendanceSessionRequest : RequestBase,
    ICommand<QueryResponse<AttendanceSessionResponse>>,
    IBoltRequest<CreateAttendanceSessionRequest, QueryResponse<AttendanceSessionResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid ContextId { get; set; }
    public Guid? PolicyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public AttendanceSessionStatus Status { get; set; } = AttendanceSessionStatus.Scheduled;
}

