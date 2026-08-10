using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Attendance.Api.Features.Reads.SessionDetail;

public static class GetAttendanceSessionDetailReadEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.AttendanceRead],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal],
        ActorRequirement = ActorRequirement.Required,
        TenantAccessMode = TenantAccessMode.ActorTenant,
        RequiredActorCapabilities = [AttendanceAuthorizationCapabilities.View])]
    [MapPost("/api/attendance/reads/session-detail", Tags = ["Attendance"],
        Summary = "Get attendance session detail",
        Capability = "view",
        RequiredActorCapabilities = [AttendanceAuthorizationCapabilities.View])]
    public static Task<Result<AttendanceSessionDetailReadResponse>> Handle(
        GetAttendanceSessionDetailReadRequest request,
        IAttendanceReadService readService,
        CancellationToken ct) =>
        readService.GetSessionDetailAsync(request, ct);
}
