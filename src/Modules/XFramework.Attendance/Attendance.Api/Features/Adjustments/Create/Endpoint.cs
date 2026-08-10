using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;

namespace Attendance.Api.Features.Adjustments.Create;

public static class CreateAttendanceAdjustmentEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.AttendanceWrite],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal])]
    [MapPost("/api/attendance/adjustments", Tags = ["Attendance"],
        Summary = "Create attendance adjustment",
        Description = "Creates an audited manual attendance correction and updates the participant session record.")]
    public static Task<Result<AttendanceAdjustmentResponse>> Handle(
        CreateAttendanceAdjustmentRequest request,
        AttendanceService attendanceService,
        CancellationToken ct) =>
        attendanceService.CreateAdjustmentAsync(request, ct);
}

