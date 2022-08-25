using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class UpdateMessageThreadHandler : CommandBaseHandler, IRequestHandler<UpdateMessageThreadCmd, CmdResponse<UpdateMessageThreadCmd>>
{
    public UpdateMessageThreadHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<UpdateMessageThreadCmd>> Handle(UpdateMessageThreadCmd request, CancellationToken cancellationToken)
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
        var updatedThread = request.Adapt(existingThread);

        if (request.EntityGuid is null)
        {
            var entity = await _dataLayer.MessageThreadEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
            if (entity is null)
            {
                return new ()
                {
                    Message = $"Message thread entity with guid {request.EntityGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedThread.Entity = entity;
        }
        
        _dataLayer.MessageThreads.Update(updatedThread);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Message thread with guid {request.Guid} updated",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}