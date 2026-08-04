using Communications.Domain.Shared.Contracts.Requests.Update;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Communications.Api.Features.Messages.UpdateDirect;

public static class UpdateDirectMessageEndpoint
{
    [BoltHandler(
        ActorRequirement = ActorRequirement.None,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.BoltService, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.SmsGateway])]
    [MapPatch("/api/communications/messages/direct", Tags = ["Messages"],
        Summary = "Update a direct message")]
    public static async Task<Result<CmdResponse>> Handle(
        UpdateMessageDirectRequest request,
        ICommunicationsService communicationsService,
        CancellationToken ct)
    {
        return await communicationsService.UpdateMessageDirectAsync(request, ct);
    }
}
