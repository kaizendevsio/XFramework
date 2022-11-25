using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class DeleteMessageDeliveryHandler : CommandBaseHandler, IRequestHandler<DeleteMessageDeliveryCmd, CmdResponse<DeleteMessageDeliveryCmd>>
{
    public DeleteMessageDeliveryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteMessageDeliveryCmd>> Handle(DeleteMessageDeliveryCmd request, CancellationToken cancellationToken)
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
        
        existingDelivery.IsDeleted = true;
        existingDelivery.IsEnabled = false;

        _dataLayer.Update(existingDelivery);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Message Delivery with Guid {request.Guid} deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}