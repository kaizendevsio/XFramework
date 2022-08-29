using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class DeleteMessageThreadEntityHandler : CommandBaseHandler, IRequestHandler<DeleteMessageThreadEntityCmd, CmdResponse<DeleteMessageThreadEntityCmd>>
{
    public DeleteMessageThreadEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteMessageThreadEntityCmd>> Handle(DeleteMessageThreadEntityCmd request, CancellationToken cancellationToken)
    {
        var existingEntity = await _dataLayer.MessageThreadEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingEntity is null)
        {
            return new ()
            {
                Message = $"Message Thread Entity with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingEntity.IsDeleted = true;
        existingEntity.IsEnabled = false;

        _dataLayer.Update(existingEntity);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Message Thread Entity with Guid {request.Guid} deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}