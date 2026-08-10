using Attendance.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Attendance.Api.Features.Reads.CredentialHistory;

public static class GetAttendanceCredentialHistoryEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.AttendanceRead],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal],
        ActorRequirement = ActorRequirement.Required,
        TenantAccessMode = TenantAccessMode.ActorTenant,
        RequiredActorCapabilities = [AttendanceAuthorizationCapabilities.View])]
    [MapPost("/api/attendance/reads/credential-history", Tags = ["Attendance"],
        Summary = "Get attendance history for credentials",
        Capability = "view",
        RequiredActorCapabilities = [AttendanceAuthorizationCapabilities.View])]
    public static Task<Result<AttendanceCredentialHistoryResponse>> Handle(
        GetAttendanceCredentialHistoryRequest request,
        IAttendanceReadService readService,
        CancellationToken ct) =>
        readService.GetCredentialHistoryAsync(request, ct);
}
