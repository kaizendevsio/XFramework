using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class UpdateMessageTypeHandler : CommandBaseHandler, IRequestHandler<UpdateMessageTypeCmd, CmdResponse<UpdateMessageTypeCmd>>
{
    public UpdateMessageTypeHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateMessageTypeCmd>> Handle(UpdateMessageTypeCmd request, CancellationToken cancellationToken)
    {
        var existingType = await _dataLayer.MessageTypes.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingType is null)
        {
            return new ()
            {
                Message = $"Message type with guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedType = request.Adapt(existingType);
        
        _dataLayer.MessageTypes.Update(updatedType);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Message type with guid {request.Guid} updated",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}