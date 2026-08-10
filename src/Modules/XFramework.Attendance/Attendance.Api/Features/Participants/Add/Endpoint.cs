using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;

namespace Attendance.Api.Features.Participants.Add;

public static class AddAttendanceParticipantEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.AttendanceWrite],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal])]
    [MapPost("/api/attendance/participants", Tags = ["Attendance"],
        Summary = "Add attendance participant",
        Description = "Adds an IdentityServer credential as a participant in an attendance context.")]
    public static Task<Result<AttendanceParticipantResponse>> Handle(
        AddAttendanceParticipantRequest request,
        AttendanceService attendanceService,
        CancellationToken ct) =>
        attendanceService.AddParticipantAsync(request, ct);
}

