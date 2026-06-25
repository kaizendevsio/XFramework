using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Attendance.Api.Features.Contexts.Create;

public static class CreateAttendanceContextEndpoint
{
    [BoltHandler]
    [MapPost("/api/attendance/contexts", Tags = ["Attendance"],
        Summary = "Create attendance context",
        Description = "Creates a tenant-scoped attendance context for school, HR, project, event, or gate attendance.")]
    public static Task<Result<AttendanceContextResponse>> Handle(
        CreateAttendanceContextRequest request,
        AttendanceService attendanceService,
        CancellationToken ct) =>
        attendanceService.CreateContextAsync(request, ct);
}

