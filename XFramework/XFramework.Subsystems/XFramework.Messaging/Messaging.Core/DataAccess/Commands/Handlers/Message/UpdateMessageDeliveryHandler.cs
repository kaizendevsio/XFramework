using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class UpdateMessageDeliveryHandler : CommandBaseHandler, IRequestHandler<UpdateMessageDeliveryCmd, CmdResponse<UpdateMessageDeliveryCmd>>
{
    public UpdateMessageDeliveryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateMessageDeliveryCmd>> Handle(UpdateMessageDeliveryCmd request, CancellationToken cancellationToken)
    {
        var existingDelivery = await _dataLayer.MessageDeliveries.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingDelivery is null)
        {
            return new ()
            {
                Message = $"Message Delivery with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedDelivery = request.Adapt(existingDelivery);

        if (request.EntityGuid is null)
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
            updatedDelivery.Entity = entity;
        }

        if (request.MessageGuid is null)
        {
            var message = await _dataLayer.Messages.FirstOrDefaultAsync(x => x.Guid == $"{request.MessageGuid}", CancellationToken.None);
            if (message is null)
            {
                return new ()
                {
                    Message = $"Message with Guid {request.MessageGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedDelivery.Message = message;
        }

        if (request.MessageThreadMemberGuid is null)
        {
            var member = await _dataLayer.MessageThreadMembers.FirstOrDefaultAsync(x => x.Guid == $"{request.MessageThreadMemberGuid}", CancellationToken.None);
            if (member is null)
            {
                return new ()
                {
                    Message = $"Message Thread Member with Guid {request.MessageThreadMemberGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedDelivery.MessageThreadMember = member;
        }

        _dataLayer.MessageDeliveries.Update(updatedDelivery);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Message Delivery with Guid {request.Guid} updated",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}