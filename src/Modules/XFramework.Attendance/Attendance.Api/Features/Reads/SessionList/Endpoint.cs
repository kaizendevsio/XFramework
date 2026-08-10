using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Attendance.Api.Features.Reads.SessionList;

public static class GetAttendanceSessionReadListEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.AttendanceRead],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal],
        ActorRequirement = ActorRequirement.Required,
        TenantAccessMode = TenantAccessMode.ActorTenant,
        RequiredActorCapabilities = [AttendanceAuthorizationCapabilities.View])]
    [MapPost("/api/attendance/reads/sessions", Tags = ["Attendance"],
        Summary = "Get attendance sessions for operator views",
        Capability = "view",
        RequiredActorCapabilities = [AttendanceAuthorizationCapabilities.View])]
    public static Task<Result<GetAttendanceSessionReadListResponse>> Handle(
        GetAttendanceSessionReadListRequest request,
        IAttendanceReadService readService,
        CancellationToken ct) =>
        readService.GetSessionsAsync(request, ct);
}
