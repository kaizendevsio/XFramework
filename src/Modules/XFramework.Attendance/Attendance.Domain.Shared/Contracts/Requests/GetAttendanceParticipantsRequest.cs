namespace Attendance.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetAttendanceParticipantsRequest : RequestBase,
    IQuery<QueryResponse<GetAttendanceParticipantsResponse>>,
    IBoltRequest<GetAttendanceParticipantsRequest, QueryResponse<GetAttendanceParticipantsResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid ContextId { get; set; }
    public Guid? CredentialId { get; set; }
    public bool? IsActive { get; set; }
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

