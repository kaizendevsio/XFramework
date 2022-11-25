using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class CreateMessageDeliveryEntityHandler : CommandBaseHandler, IRequestHandler<CreateMessageDeliveryEntityCmd, CmdResponse<CreateMessageDeliveryEntityCmd>>
{
    public CreateMessageDeliveryEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateMessageDeliveryEntityCmd>> Handle(CreateMessageDeliveryEntityCmd request, CancellationToken cancellationToken)
    {
        var entity = request.Adapt<MessageDeliveryEntity>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";

        await _dataLayer.MessageDeliveryEntities.AddAsync(entity, CancellationToken.None);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(entity.Guid);
        return new()
        {
            Message = $"Message Delivery Entity with guid {entity.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}