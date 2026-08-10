using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;

namespace Attendance.Api.Features.Sessions.GetList;

public static class GetAttendanceSessionsEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.AttendanceRead],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal])]
    [MapGet("/api/attendance/sessions", Tags = ["Attendance"],
        Summary = "Get attendance sessions",
        Description = "Retrieves paginated attendance sessions for a context.")]
    public static Task<Result<GetAttendanceSessionsResponse>> Handle(
        GetAttendanceSessionsRequest request,
        AttendanceService attendanceService,
        CancellationToken ct) =>
        attendanceService.GetSessionsAsync(request, ct);
}

