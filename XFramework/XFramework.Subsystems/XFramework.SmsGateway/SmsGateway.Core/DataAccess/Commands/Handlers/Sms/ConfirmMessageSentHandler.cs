using Messaging.Integration.Interfaces;
using SmsGateway.Core.DataAccess.Commands.Entity.Sms;

namespace SmsGateway.Core.DataAccess.Commands.Handlers.Sms;

public class ConfirmMessageSentHandler : CommandBaseHandler, IRequestHandler<ConfirmSmsMessageSentCmd, CmdResponse>
{
    private readonly IMessagingNodeServiceWrapper _messagingNodeServiceWrapper;

    public ConfirmMessageSentHandler(ICachingService cachingService, IMessagingNodeServiceWrapper messagingNodeServiceWrapper)
    {
        _messagingNodeServiceWrapper = messagingNodeServiceWrapper;
        _cachingService = cachingService;
    }
    
    public async Task<CmdResponse> Handle(ConfirmSmsMessageSentCmd request, CancellationToken cancellationToken)
    {
        var item = _cachingService.PendingMessageList.SingleOrDefault(i => i.Guid == $"{request.Guid}");
        _cachingService.PendingMessageList.Remove(item);
        
        await _messagingNodeServiceWrapper.ConfirmMessageSent(new()
        {
            Guid = request.Guid
        });

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}