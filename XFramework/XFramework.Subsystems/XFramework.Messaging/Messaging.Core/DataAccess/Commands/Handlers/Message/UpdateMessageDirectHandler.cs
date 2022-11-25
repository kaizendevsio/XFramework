using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class UpdateMessageDirectHandler : CommandBaseHandler, IRequestHandler<UpdateMessageDirectCmd, CmdResponse<UpdateMessageDirectCmd>>
{
    public UpdateMessageDirectHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateMessageDirectCmd>> Handle(UpdateMessageDirectCmd request, CancellationToken cancellationToken)
    {
        var existingDirect = await _dataLayer.MessageDirects.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingDirect is null)
        {
            return new ()
            {
                Message = $"Message Direct with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedDirect = request.Adapt(existingDirect);

        if (request.ParentMessageGuid is null)
        {
            var parentMessage = await _dataLayer.MessageDirects.FirstOrDefaultAsync(x => x.Guid == $"{request.ParentMessageGuid}", CancellationToken.None);
            if (parentMessage is null)
            {
                return new ()
                {
                    Message = $"Message with guid {request.ParentMessageGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedDirect.ParentMessage = parentMessage;
        }

        if (request.TypeGuid is null)
        {
            var type = await _dataLayer.MessageTypes.FirstOrDefaultAsync(x => x.Guid == $"{request.TypeGuid}", CancellationToken.None);
            if (type is null)
            {
                return new ()
                {
                    Message = $"Message type with guid {request.TypeGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedDirect.Type = type;
        }
        
        _dataLayer.MessageDirects.Update(updatedDirect);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Message Direct with Guid {request.Guid} updated",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}