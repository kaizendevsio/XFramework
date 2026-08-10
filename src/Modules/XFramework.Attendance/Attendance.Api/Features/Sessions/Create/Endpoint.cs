using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;

namespace Attendance.Api.Features.Sessions.Create;

public static class CreateAttendanceSessionEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.AttendanceWrite],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal])]
    [MapPost("/api/attendance/sessions", Tags = ["Attendance"],
        Summary = "Create attendance session",
        Description = "Creates a concrete attendance window inside an attendance context.")]
    public static Task<Result<AttendanceSessionResponse>> Handle(
        CreateAttendanceSessionRequest request,
        AttendanceService attendanceService,
        CancellationToken ct) =>
        attendanceService.CreateSessionAsync(request, ct);
}

