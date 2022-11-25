using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class DeleteMessageDeliveryEntityHandler : CommandBaseHandler, IRequestHandler<DeleteMessageDeliveryEntityCmd, CmdResponse<DeleteMessageDeliveryEntityCmd>>
{
    public DeleteMessageDeliveryEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteMessageDeliveryEntityCmd>> Handle(DeleteMessageDeliveryEntityCmd request, CancellationToken cancellationToken)
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
        
        existingEntity.IsDeleted = true;
        existingEntity.IsEnabled = false;

        _dataLayer.Update(existingEntity);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Message Delivery Entity with Guid {request.Guid} deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}