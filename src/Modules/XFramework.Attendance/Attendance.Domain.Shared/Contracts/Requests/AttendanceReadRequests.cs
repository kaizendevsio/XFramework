namespace Attendance.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetAttendanceContextOverviewRequest : RequestBase,
    IQuery<QueryResponse<GetAttendanceContextOverviewResponse>>,
    IBoltRequest<GetAttendanceContextOverviewRequest, QueryResponse<GetAttendanceContextOverviewResponse>>
{
    public Guid? TenantId { get; set; }
    public int Limit { get; set; } = 500;
}

[MemoryPackable]
public partial record GetAttendanceSessionReadListRequest : RequestBase,
    IQuery<QueryResponse<GetAttendanceSessionReadListResponse>>,
    IBoltRequest<GetAttendanceSessionReadListRequest, QueryResponse<GetAttendanceSessionReadListResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid? ContextId { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public AttendanceSessionStatus? Status { get; set; }
    public int Limit { get; set; } = 500;
}

[MemoryPackable]
public partial record GetAttendanceSessionDetailReadRequest : RequestBase,
    IQuery<QueryResponse<AttendanceSessionDetailReadResponse>>,
    IBoltRequest<GetAttendanceSessionDetailReadRequest, QueryResponse<AttendanceSessionDetailReadResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid SessionId { get; set; }
}

[MemoryPackable]
public partial record GetAttendanceParticipantReadListRequest : RequestBase,
    IQuery<QueryResponse<GetAttendanceParticipantReadListResponse>>,
    IBoltRequest<GetAttendanceParticipantReadListRequest, QueryResponse<GetAttendanceParticipantReadListResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid ContextId { get; set; }
    public int Limit { get; set; } = 1000;
}

[MemoryPackable]
public partial record GetAttendanceCredentialHistoryRequest : RequestBase,
    IQuery<QueryResponse<AttendanceCredentialHistoryResponse>>,
    IBoltRequest<GetAttendanceCredentialHistoryRequest, QueryResponse<AttendanceCredentialHistoryResponse>>
{
    public Guid? TenantId { get; set; }
    public List<Guid> CredentialIds { get; set; } = [];
}
