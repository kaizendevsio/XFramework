using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Attendance.Api.Features.Reads.ParticipantList;

public static class GetAttendanceParticipantReadListEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.AttendanceRead],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal],
        ActorRequirement = ActorRequirement.Required,
        TenantAccessMode = TenantAccessMode.ActorTenant,
        RequiredActorCapabilities = [AttendanceAuthorizationCapabilities.View])]
    [MapPost("/api/attendance/reads/participants", Tags = ["Attendance"],
        Summary = "Get attendance participants for operator views",
        Capability = "view",
        RequiredActorCapabilities = [AttendanceAuthorizationCapabilities.View])]
    public static Task<Result<GetAttendanceParticipantReadListResponse>> Handle(
        GetAttendanceParticipantReadListRequest request,
        IAttendanceReadService readService,
        CancellationToken ct) =>
        readService.GetParticipantsAsync(request, ct);
}
