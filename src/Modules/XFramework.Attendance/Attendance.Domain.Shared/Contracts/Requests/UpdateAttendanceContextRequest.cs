namespace Attendance.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record UpdateAttendanceContextRequest : RequestBase,
    ICommand<QueryResponse<AttendanceContextResponse>>,
    IBoltRequest<UpdateAttendanceContextRequest, QueryResponse<AttendanceContextResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid ContextId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public AttendanceContextType ContextType { get; set; }
    public string? Description { get; set; }
    public Guid? DefaultPolicyId { get; set; }
    public bool IsActive { get; set; } = true;
}

