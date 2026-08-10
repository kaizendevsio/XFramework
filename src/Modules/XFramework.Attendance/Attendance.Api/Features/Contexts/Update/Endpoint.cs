using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;

namespace Attendance.Api.Features.Contexts.Update;

public static class UpdateAttendanceContextEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.AttendanceWrite],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal])]
    [MapPut("/api/attendance/contexts", Tags = ["Attendance"],
        Summary = "Update attendance context",
        Description = "Updates a tenant-scoped attendance context.")]
    public static Task<Result<AttendanceContextResponse>> Handle(
        UpdateAttendanceContextRequest request,
        AttendanceService attendanceService,
        CancellationToken ct) =>
        attendanceService.UpdateContextAsync(request, ct);
}

