namespace Attendance.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record AttendanceContextOverviewResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public AttendanceContextType ContextType { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int ActiveParticipantCount { get; set; }
    public int SessionCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

[MemoryPackable]
public partial record GetAttendanceContextOverviewResponse
{
    public List<AttendanceContextOverviewResponse> Items { get; set; } = [];
}

[MemoryPackable]
public partial record GetAttendanceSessionReadListResponse
{
    public List<AttendanceSessionResponse> Items { get; set; } = [];
    public List<AttendanceContextResponse> Contexts { get; set; } = [];
}

[MemoryPackable]
public partial record AttendanceSessionDetailReadResponse
{
    public AttendanceSessionResponse Session { get; set; } = new();
    public AttendanceContextResponse? Context { get; set; }
    public List<AttendanceParticipantResponse> Participants { get; set; } = [];
    public List<AttendanceRecordResponse> Records { get; set; } = [];
    public List<AttendanceEventResponse> RecentEvents { get; set; } = [];
}

[MemoryPackable]
public partial record GetAttendanceParticipantReadListResponse
{
    public List<AttendanceParticipantResponse> Items { get; set; } = [];
}

[MemoryPackable]
public partial record AttendanceCredentialHistoryResponse
{
    public List<AttendanceParticipantResponse> Participants { get; set; } = [];
    public List<AttendanceRecordResponse> Records { get; set; } = [];
    public List<AttendanceSessionResponse> Sessions { get; set; } = [];
    public List<AttendanceContextResponse> Contexts { get; set; } = [];
}
