namespace Attendance.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record AddAttendanceParticipantRequest : RequestBase,
    ICommand<QueryResponse<AttendanceParticipantResponse>>,
    IBoltRequest<AddAttendanceParticipantRequest, QueryResponse<AttendanceParticipantResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid ContextId { get; set; }
    public Guid CredentialId { get; set; }
    public string? DisplayName { get; set; }
    public string? ReferenceCode { get; set; }
    public DateTime? StartedAt { get; set; }
}

