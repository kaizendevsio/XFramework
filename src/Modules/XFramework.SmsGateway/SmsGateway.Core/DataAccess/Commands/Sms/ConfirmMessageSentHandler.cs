using Messaging.Integration.Drivers;
using Microsoft.Extensions.Logging;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Enums;

namespace SmsGateway.Core.DataAccess.Commands.Sms;
public record ConfirmSmsMessageSentRequest(Guid Id) : RequestBase, IRequest<CmdResponse>;

public class ConfirmMessageSentHandler(
    ILogger<ConfirmMessageSentHandler> logger,
    ICachingService cachingService,
    IMessagingServiceWrapper messagingServiceWrapper
    )
    : IRequestHandler<ConfirmSmsMessageSentRequest, CmdResponse>
{

    public async Task<CmdResponse> Handle(ConfirmSmsMessageSentRequest request, CancellationToken cancellationToken)
    {
        var item = cachingService.PendingMessageList
            .FirstOrDefault(i => i.Value.Id == request.Id);
        
        if (item.Value is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var retryCount = 0;
        
        retry:
        var result = await messagingServiceWrapper.ConfirmMessageSent(new()
        {
            Metadata = request.Metadata,
            Id = item.Value.Id,
            AgentClusterId = item.Value.AgentClusterId,
            SentAt = DateTime.Now.ToUniversalTime()
        });

        if (result.IsSuccess is false)
        {
            retryCount++;
            await Task.Delay(1500, CancellationToken.None);
            logger.LogWarning("Failed to confirm message sent, reason: {Reason}, retry count: {RetryCount}", result.Message, retryCount);
            goto retry;
        }

        cachingService.PendingMessageList.Remove(item.Key, out _);
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}