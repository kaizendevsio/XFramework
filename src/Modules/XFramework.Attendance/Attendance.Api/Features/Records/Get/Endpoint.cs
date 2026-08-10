using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;

namespace Attendance.Api.Features.Records.Get;

public static class GetAttendanceRecordEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.AttendanceRead],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal])]
    [MapGet("/api/attendance/records", Tags = ["Attendance"],
        Summary = "Get attendance record",
        Description = "Gets the session-level attendance record for one participant.")]
    public static Task<Result<AttendanceRecordResponse>> Handle(
        GetAttendanceRecordRequest request,
        AttendanceService attendanceService,
        CancellationToken ct) =>
        attendanceService.GetRecordAsync(request, ct);
}

