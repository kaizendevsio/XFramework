using Communications.Domain.Shared.Contracts.Requests.Create;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Communications.Api.Features.Messages.CreateDirect;

public static class CreateDirectMessageEndpoint
{
    [BoltHandler(
        ActorRequirement = ActorRequirement.Optional,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.BoltService, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal, XFrameworkServiceNames.IdentityServer])]
    [MapPost("/api/communications/messages/direct", Tags = ["Messages"],
        Summary = "Create and send a direct message")]
    public static async Task<Result<CmdResponse>> Handle(
        CreateDirectMessageRequest request,
        ICommunicationsService communicationsService,
        CancellationToken ct)
    {
        return await communicationsService.CreateDirectMessageAsync(request, ct);
    }
}
