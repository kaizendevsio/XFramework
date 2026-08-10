using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;

namespace Attendance.Api.Features.Events.Record;

public static class RecordAttendanceEventEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.AttendanceWrite],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal])]
    [MapPost("/api/attendance/events", Tags = ["Attendance"],
        Summary = "Record attendance event",
        Description = "Records an idempotent check-in or check-out event and updates the session attendance record.")]
    public static Task<Result<AttendanceEventResponse>> Handle(
        RecordAttendanceEventRequest request,
        AttendanceService attendanceService,
        CancellationToken ct) =>
        attendanceService.RecordEventAsync(request, ct);
}

