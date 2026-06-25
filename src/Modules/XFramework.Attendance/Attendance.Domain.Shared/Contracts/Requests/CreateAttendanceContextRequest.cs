namespace Attendance.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record CreateAttendanceContextRequest : RequestBase,
    ICommand<QueryResponse<AttendanceContextResponse>>,
    IBoltRequest<CreateAttendanceContextRequest, QueryResponse<AttendanceContextResponse>>
{
    public Guid? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public AttendanceContextType ContextType { get; set; }
    public string? Description { get; set; }
    public Guid? DefaultPolicyId { get; set; }
    public bool IsActive { get; set; } = true;
}

