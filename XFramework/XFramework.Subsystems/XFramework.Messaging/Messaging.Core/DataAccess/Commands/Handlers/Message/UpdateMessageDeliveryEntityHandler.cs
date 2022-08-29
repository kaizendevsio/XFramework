using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class UpdateMessageDeliveryEntityHandler : CommandBaseHandler, IRequestHandler<UpdateMessageDeliveryEntityCmd, CmdResponse<UpdateMessageDeliveryEntityCmd>>
{
    public UpdateMessageDeliveryEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateMessageDeliveryEntityCmd>> Handle(UpdateMessageDeliveryEntityCmd request, CancellationToken cancellationToken)
    {
        var existingEntity = await _dataLayer.MessageDeliveryEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingEntity is null)
        {
            return new ()
            {
                Message = $"Message Delivery Entity with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedEntity = request.Adapt(existingEntity);

        _dataLayer.MessageDeliveryEntities.Update(updatedEntity);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Message Delivery Entity with Guid {request.Guid} updated",
            HttpStatusCode = HttpStatusCode.OK
        };

    }
}