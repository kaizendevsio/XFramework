using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Attendance.Api.Features.Reads.ContextOverview;

public static class GetAttendanceContextOverviewEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.AttendanceRead],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal],
        ActorRequirement = ActorRequirement.Required,
        TenantAccessMode = TenantAccessMode.ActorTenant,
        RequiredActorCapabilities = [AttendanceAuthorizationCapabilities.View])]
    [MapPost("/api/attendance/reads/context-overview", Tags = ["Attendance"],
        Summary = "Get attendance context overview",
        Capability = "view",
        RequiredActorCapabilities = [AttendanceAuthorizationCapabilities.View])]
    public static Task<Result<GetAttendanceContextOverviewResponse>> Handle(
        GetAttendanceContextOverviewRequest request,
        IAttendanceReadService readService,
        CancellationToken ct) =>
        readService.GetContextOverviewAsync(request, ct);
}
