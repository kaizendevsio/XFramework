namespace Attendance.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record GetAttendanceParticipantsResponse
{
    public List<AttendanceParticipantResponse> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

