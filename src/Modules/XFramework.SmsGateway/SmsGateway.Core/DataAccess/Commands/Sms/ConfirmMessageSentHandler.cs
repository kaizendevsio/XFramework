using Messaging.Integration.Drivers;
using SmsGateway.Domain.Shared.Contracts.Requests.Update;

namespace SmsGateway.Core.DataAccess.Commands.Sms;

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
        var item = _cachingService.PendingMessageList.SingleOrDefault(i => i.Guid == $"{request.Id}");
        _cachingService.PendingMessageList.Remove(item);
        
        await _messagingNodeServiceWrapper.ConfirmMessageSent(new()
        {
            Guid = request.Id
        });

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}