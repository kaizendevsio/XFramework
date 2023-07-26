using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class CreateMessageThreadEntityHandler : CommandBaseHandler, IRequestHandler<CreateMessageThreadEntityCmd, CmdResponse<CreateMessageThreadEntityCmd>>
{
    public CreateMessageThreadEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateMessageThreadEntityCmd>> Handle(CreateMessageThreadEntityCmd request, CancellationToken cancellationToken)
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

        var entity = request.Adapt<MessageThreadEntity>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.MessageType = type;
        
        await _dataLayer.MessageThreadEntities.AddAsync(entity, CancellationToken.None);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(entity.Guid);
        return new()
        {
            Message = $"Message thread entity with guid {entity.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
        };
    }
}