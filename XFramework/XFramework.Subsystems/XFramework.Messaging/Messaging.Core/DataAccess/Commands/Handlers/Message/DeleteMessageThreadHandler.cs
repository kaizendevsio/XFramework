using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class DeleteMessageThreadHandler : CommandBaseHandler, IRequestHandler<DeleteMessageThreadCmd, CmdResponse<DeleteMessageThreadCmd>>
{
    public DeleteMessageThreadHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<DeleteMessageThreadCmd>> Handle(DeleteMessageThreadCmd request, CancellationToken cancellationToken)
    {
        var existingThread = await _dataLayer.MessageThreads.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingThread is null)
        {
            return new ()
            {
                Message = $"Message thread with guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingThread.IsDeleted = true;
        existingThread.IsEnabled = false;
        
        _dataLayer.MessageThreads.Update(existingThread);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Message thread with guid {request.Guid} deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}