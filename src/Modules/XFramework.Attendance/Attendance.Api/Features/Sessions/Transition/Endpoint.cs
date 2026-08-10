using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;

namespace Attendance.Api.Features.Sessions.Transition;

public static class TransitionAttendanceSessionEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.AttendanceWrite],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal])]
    [MapPost("/api/attendance/sessions/status", Tags = ["Attendance"],
        Summary = "Transition attendance session status",
        Description = "Opens, closes, or cancels an attendance session using the allowed lifecycle transitions.")]
    public static Task<Result<AttendanceSessionResponse>> Handle(
        TransitionAttendanceSessionRequest request,
        AttendanceService attendanceService,
        CancellationToken ct) =>
        attendanceService.TransitionSessionAsync(request, ct);
}
