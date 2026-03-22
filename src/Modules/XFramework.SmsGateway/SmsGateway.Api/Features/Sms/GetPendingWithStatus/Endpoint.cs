using SmsGateway.Api.Services;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace SmsGateway.Api.Features.Sms.GetPendingWithStatus;

public static class GetPendingWithStatusUpdateEndpoint
{
    [MapGet("/api/SmsGatewayNode/List/{agentClusterId:guid}", Tags = ["SMS"],
        Summary = "Get pending messages and set to processing",
        Description = "Gets pending SMS messages and updates their status to Processing. This endpoint is used by SMS gateway nodes.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<List<SmsNodeJob>>> Handle(
        Guid agentClusterId,
        ISmsService smsService,
        CancellationToken ct)
    {
        return await smsService.GetPendingWithStatusUpdateAsync(agentClusterId, ct);
    }
}
