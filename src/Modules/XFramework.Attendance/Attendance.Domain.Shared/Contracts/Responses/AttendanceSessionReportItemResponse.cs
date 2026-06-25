namespace Attendance.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record AttendanceSessionReportItemResponse
{
    public Guid SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int PresentCount { get; set; }
    public int LateCount { get; set; }
    public int AbsentCount { get; set; }
    public int IncompleteCount { get; set; }
    public int ManualAdjustedCount { get; set; }
    public int ExcusedCount { get; set; }
}

