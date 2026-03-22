using SmsGateway.Api.Services;
using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace SmsGateway.Api.Features.Sms.GetScheduled;

public static class GetScheduledSmsMessagesEndpoint
{
    [MapGet("/api/sms/messages/scheduled/{agentClusterId:guid}", Tags = ["SMS"],
        Summary = "Get scheduled SMS messages",
        Description = "Gets a list of scheduled SMS messages for a specific agent cluster",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<List<SmsNodeJob>>> Handle(
        Guid agentClusterId,
        ISmsService smsService,
        CancellationToken ct)
    {
        var request = new GetScheduledSmsMessageListRequest
        {
            AgentClusterId = agentClusterId
        };

        return await smsService.GetScheduledSmsMessagesAsync(request, ct);
    }
}
