using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;

namespace Attendance.Api.Features.Contexts.GetList;

public static class GetAttendanceContextsEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.AttendanceRead],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal])]
    [MapGet("/api/attendance/contexts", Tags = ["Attendance"],
        Summary = "Get attendance contexts",
        Description = "Retrieves paginated tenant-scoped attendance contexts.")]
    public static Task<Result<GetAttendanceContextsResponse>> Handle(
        GetAttendanceContextsRequest request,
        AttendanceService attendanceService,
        CancellationToken ct) =>
        attendanceService.GetContextsAsync(request, ct);
}

