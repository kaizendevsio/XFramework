using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class CreateMessageTypeHandler : CommandBaseHandler, IRequestHandler<CreateMessageTypeCmd, CmdResponse<CreateMessageTypeCmd>>
{
    public CreateMessageTypeHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateMessageTypeCmd>> Handle(CreateMessageTypeCmd request, CancellationToken cancellationToken)
    {
        var type = request.Adapt<MessageType>();
        type.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";

        await _dataLayer.MessageTypes.AddAsync(type, CancellationToken.None);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(type.Guid);
        return new()
        {
            Message = $"Message type with guid {type.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
        };
    }
}