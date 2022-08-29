using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class DeleteMessageTypeHandler : CommandBaseHandler, IRequestHandler<DeleteMessageTypeCmd, CmdResponse<DeleteMessageTypeCmd>>
{
    public DeleteMessageTypeHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteMessageTypeCmd>> Handle(DeleteMessageTypeCmd request, CancellationToken cancellationToken)
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
        
        existingType.IsDeleted = true;
        existingType.IsEnabled = false;

        _dataLayer.Update(existingType);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Message type with guid {request.Guid} deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}