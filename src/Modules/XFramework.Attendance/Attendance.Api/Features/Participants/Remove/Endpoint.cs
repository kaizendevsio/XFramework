using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Attendance.Api.Features.Participants.Remove;

public static class RemoveAttendanceParticipantEndpoint
{
    [BoltHandler]
    [MapDelete("/api/attendance/participants", Tags = ["Attendance"],
        Summary = "Remove attendance participant",
        Description = "Soft-removes a participant from an attendance context.")]
    public static Task<Result> Handle(
        RemoveAttendanceParticipantRequest request,
        AttendanceService attendanceService,
        CancellationToken ct) =>
        attendanceService.RemoveParticipantAsync(request, ct);
}

