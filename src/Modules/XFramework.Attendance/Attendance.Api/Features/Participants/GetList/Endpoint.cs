using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Attendance.Api.Features.Participants.GetList;

public static class GetAttendanceParticipantsEndpoint
{
    [BoltHandler]
    [MapGet("/api/attendance/participants", Tags = ["Attendance"],
        Summary = "Get attendance participants",
        Description = "Retrieves paginated participants for an attendance context.")]
    public static Task<Result<GetAttendanceParticipantsResponse>> Handle(
        GetAttendanceParticipantsRequest request,
        AttendanceService attendanceService,
        CancellationToken ct) =>
        attendanceService.GetParticipantsAsync(request, ct);
}

