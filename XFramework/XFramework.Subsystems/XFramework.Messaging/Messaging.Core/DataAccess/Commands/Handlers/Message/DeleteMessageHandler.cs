using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class DeleteMessageHandler : CommandBaseHandler, IRequestHandler<DeleteMessageCmd, CmdResponse<DeleteMessageCmd>>
{
    public DeleteMessageHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<DeleteMessageCmd>> Handle(DeleteMessageCmd request, CancellationToken cancellationToken)
    {
        var existingMessage = await _dataLayer.Messages.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingMessage is null)
        {
            return new ()
            {
                Message = $"Message with guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingMessage.IsDeleted = true;
        existingMessage.IsEnabled = false;

        _dataLayer.Messages.Update(existingMessage);
        await _dataLayer.SaveChangesAsync(cancellationToken);
        
        return new ()
        {
            Message = $"Message with guid {request.Guid} deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}