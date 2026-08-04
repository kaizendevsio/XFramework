using SmsGateway.Api.Services;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace SmsGateway.Api.Features.Sms.GetPendingWithStatus;

public static class GetPendingWithStatusUpdateEndpoint
{
    [MapGet("/api/SmsGatewayNode/List/{agentClusterId:guid}", Tags = ["SMS"],
        Summary = "Get pending messages and set to processing",
        Description = "Gets pending SMS messages and updates their status to Processing. This endpoint is used by SMS gateway nodes.",
        ExcludeFromOpenApi = true,
        ActorRequirement = ActorRequirement.None,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.SmsGatewayAgent, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.SmsGateway])]
    public static async Task<Result<List<SmsNodeJob>>> Handle(
        GetPendingWithStatusUpdateRequest request,
        ISmsService smsService,
        CancellationToken ct)
    {
        return await smsService.GetPendingWithStatusUpdateAsync(request.AgentClusterId, ct);
    }
}

public sealed record GetPendingWithStatusUpdateRequest : RequestBase
{
    public Guid AgentClusterId { get; init; }
}
