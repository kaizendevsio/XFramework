using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;

namespace Attendance.Api.Features.Reports.GetContextRange;

public static class GetAttendanceReportEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.AttendanceRead],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal])]
    [MapGet("/api/attendance/reports/context-range", Tags = ["Attendance"],
        Summary = "Get attendance report",
        Description = "Gets paginated session attendance summaries for a context and UTC date range.")]
    public static Task<Result<AttendanceReportResponse>> Handle(
        GetAttendanceReportRequest request,
        AttendanceService attendanceService,
        CancellationToken ct) =>
        attendanceService.GetReportAsync(request, ct);
}

