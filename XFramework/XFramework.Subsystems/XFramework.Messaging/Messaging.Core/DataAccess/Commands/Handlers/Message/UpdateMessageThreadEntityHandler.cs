using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class UpdateMessageThreadEntityHandler : CommandBaseHandler, IRequestHandler<UpdateMessageThreadEntityCmd, CmdResponse<UpdateMessageThreadEntityCmd>>
{
    public UpdateMessageThreadEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateMessageThreadEntityCmd>> Handle(UpdateMessageThreadEntityCmd request, CancellationToken cancellationToken)
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
        var updatedEntity = request.Adapt(existingEntity);

        if (request.MessageTypeGuid is null)
        {
            var type = await _dataLayer.MessageTypes.FirstOrDefaultAsync(x => x.Guid == $"{request.MessageTypeGuid}", CancellationToken.None);
            if (type is null)
            {
                return new ()
                {
                    Message = $"Message type with guid {request.MessageTypeGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedEntity.MessageType = type;
        }
        
        _dataLayer.MessageThreadEntities.Update(updatedEntity);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Message Thread Entity with Guid {request.Guid} updated",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}