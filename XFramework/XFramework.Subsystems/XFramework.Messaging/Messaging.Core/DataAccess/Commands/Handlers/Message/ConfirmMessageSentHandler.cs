using Messaging.Core.DataAccess.Commands.Entity.Message;
using Messaging.Domain.Generic.Enums;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class ConfirmMessageSentHandler : CommandBaseHandler, IRequestHandler<ConfirmMessageSentCmd, CmdResponse>
{
    private readonly ICachingService _cachingService;

    public ConfirmMessageSentHandler(IDataLayer dataLayer, ICachingService cachingService)
    {
        _cachingService = cachingService;
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse> Handle(ConfirmMessageSentCmd request, CancellationToken cancellationToken)
    {
        var message = _cachingService.QueuedMessageList.FirstOrDefault(i => i.Guid == $"{request.Guid}");
        if (message is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NotFound,
                
            };
        }

        reCheck:
        if (message.Status == (int) MessageStatus.Queued)
        {
            await Task.Delay(100, CancellationToken.None);
            message = _cachingService.QueuedMessageList.FirstOrDefault(i => i.Guid == $"{request.Guid}");
            goto reCheck;
        }

        var messageFromDb = await _dataLayer.MessageDirects.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", CancellationToken.None);
        messageFromDb.Status = (short) MessageStatus.Sent;

        _dataLayer.MessageDirects.Update(messageFromDb);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}