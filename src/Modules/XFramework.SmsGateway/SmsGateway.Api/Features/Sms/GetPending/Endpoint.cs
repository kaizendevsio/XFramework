using SmsGateway.Api.Services;
using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace SmsGateway.Api.Features.Sms.GetPending;

public static class GetPendingSmsMessagesEndpoint
{
    [MapGet("/api/sms/messages/pending/{agentClusterId:guid}", Tags = ["SMS"],
        Summary = "Get pending SMS messages",
        Description = "Gets a list of pending SMS messages for a specific agent cluster",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<List<SmsNodeJob>>> Handle(
        Guid agentClusterId,
        ISmsService smsService,
        CancellationToken ct)
    {
        var request = new GetPendingSmsMessageListRequest
        {
            AgentClusterId = agentClusterId
        };

        return await smsService.GetPendingSmsMessagesAsync(request, ct);
    }
}
