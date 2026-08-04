using SmsGateway.Api.Services;
using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace SmsGateway.Api.Features.Sms.GetScheduled;

public static class GetScheduledSmsMessagesEndpoint
{
    [BoltHandler(
        ActorRequirement = ActorRequirement.None,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.SmsGatewayAgent, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.SmsGateway])]
    [MapGet("/api/sms/messages/scheduled/{agentClusterId:guid}", Tags = ["SMS"],
        Summary = "Get scheduled SMS messages",
        Description = "Gets a list of scheduled SMS messages for a specific agent cluster",
        ExcludeFromOpenApi = true,
        ActorRequirement = ActorRequirement.None,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.SmsGatewayAgent, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.SmsGateway])]
    public static async Task<Result<List<SmsNodeJob>>> Handle(
        GetScheduledSmsMessageListRequest request,
        ISmsService smsService,
        CancellationToken ct)
    {
        return await smsService.GetScheduledSmsMessagesAsync(request, ct);
    }
}
