using SmsGateway.Api.Services;
using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace SmsGateway.Api.Features.Sms.GetPending;

public static class GetPendingSmsMessagesEndpoint
{
    [BoltHandler(
        ActorRequirement = ActorRequirement.None,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.SmsGatewayAgent, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.SmsGateway])]
    [MapGet("/api/sms/messages/pending/{agentClusterId:guid}", Tags = ["SMS"],
        Summary = "Get pending SMS messages",
        Description = "Gets a list of pending SMS messages for a specific agent cluster",
        ExcludeFromOpenApi = true,
        ActorRequirement = ActorRequirement.None,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.SmsGatewayAgent, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.SmsGateway])]
    public static async Task<Result<List<SmsNodeJob>>> Handle(
        GetPendingSmsMessageListRequest request,
        ISmsService smsService,
        CancellationToken ct)
    {
        return await smsService.GetPendingSmsMessagesAsync(request, ct);
    }
}
