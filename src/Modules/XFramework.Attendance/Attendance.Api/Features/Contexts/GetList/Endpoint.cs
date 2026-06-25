using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Attendance.Api.Features.Contexts.GetList;

public static class GetAttendanceContextsEndpoint
{
    [BoltHandler]
    [MapGet("/api/attendance/contexts", Tags = ["Attendance"],
        Summary = "Get attendance contexts",
        Description = "Retrieves paginated tenant-scoped attendance contexts.")]
    public static Task<Result<GetAttendanceContextsResponse>> Handle(
        GetAttendanceContextsRequest request,
        AttendanceService attendanceService,
        CancellationToken ct) =>
        attendanceService.GetContextsAsync(request, ct);
}

