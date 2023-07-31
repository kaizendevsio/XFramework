using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class CreateMessageDeliveryHandler : CommandBaseHandler, IRequestHandler<CreateMessageDeliveryCmd, CmdResponse<CreateMessageDeliveryCmd>>
{
    public CreateMessageDeliveryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateMessageDeliveryCmd>> Handle(CreateMessageDeliveryCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.MessageDeliveryEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
        if (entity is null)
        {
            return new ()
            {
                Message = $"Entity with Guid {request.EntityGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var message = await _dataLayer.Messages.FirstOrDefaultAsync(x => x.Guid == $"{request.MessageGuid}", CancellationToken.None);
        if (message is null)
        {
            return new ()
            {
                Message = $"Message with Guid {request.MessageGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var member = await _dataLayer.MessageThreadMembers.FirstOrDefaultAsync(x => x.Guid == $"{request.MessageThreadMemberGuid}", CancellationToken.None);
        if (member is null)
        {
            return new ()
            {
                Message = $"Message Thread Member with Guid {request.MessageThreadMemberGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var delivery = request.Adapt<MessageDelivery>();
        delivery.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        delivery.Message = message;
        delivery.MessageThreadMember = member;
        delivery.Entity = entity;
        
        await _dataLayer.MessageDeliveries.AddAsync(delivery, CancellationToken.None);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(delivery.Guid);
        return new()
        {
            Message = $"Message Delivery with Guid {delivery.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
        };
    }
}