namespace Attendance.Domain.Shared.Enums;

public enum AttendanceRecordStatus
{
    Unknown = 0,
    Present = 1,
    Late = 2,
    Absent = 3,
    Incomplete = 4,
    Excused = 5,
    ManualAdjusted = 6
}

