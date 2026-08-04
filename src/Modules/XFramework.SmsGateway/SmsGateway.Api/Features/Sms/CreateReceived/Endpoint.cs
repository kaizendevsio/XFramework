using SmsGateway.Api.Services;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace SmsGateway.Api.Features.Sms.CreateReceived;

public static class CreateMessageReceivedEndpoint
{
    [MapPost("/api/sms/messages/received", Tags = ["SMS"],
        Summary = "Create received message record",
        Description = "Creates a record of a received SMS message",
        ExcludeFromOpenApi = true,
        ActorRequirement = ActorRequirement.None,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.SmsGatewayAgent, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.SmsGateway])]
    public static async Task<Result<CmdResponse>> Handle(
        CreateMessageReceivedRequest request,
        ISmsService smsService,
        CancellationToken ct)
    {
        return await smsService.CreateMessageReceivedAsync(request, ct);
    }
}
