using Messaging.Integration.Drivers;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Enums;

namespace SmsGateway.Core.DataAccess.Commands.Sms;
public record ConfirmSmsMessageSentRequest(Guid Id) : RequestBase, IRequest<CmdResponse>;

public class ConfirmMessageSentHandler : IRequestHandler<ConfirmSmsMessageSentRequest, CmdResponse>
{
    private readonly ICachingService _cachingService;
    private readonly IMessagingNodeServiceWrapper _messagingNodeServiceWrapper;

    public ConfirmMessageSentHandler(ICachingService cachingService, IMessagingNodeServiceWrapper messagingNodeServiceWrapper)
    {
        _cachingService = cachingService;
        _messagingNodeServiceWrapper = messagingNodeServiceWrapper;
    }
    
    public async Task<CmdResponse> Handle(ConfirmSmsMessageSentRequest request, CancellationToken cancellationToken)
    {
        var item = _cachingService.PendingMessageList
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
        var result = await _messagingNodeServiceWrapper.ConfirmMessageSent(new()
        {
            Id = item.Value.Id
        });

        if (result.IsSuccess is false && retryCount < 3)
        {
            retryCount++;
            await Task.Delay(1000, CancellationToken.None);
            goto retry;
        }
        
        _cachingService.PendingMessageList.Remove(item.Key, out _);

        return new()
        {
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}