using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class CreateDirectMessageHandler : CommandBaseHandler ,IRequestHandler<CreateDirectMessageCmd, CmdResponse>
{
    public CreateDirectMessageHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse> Handle(CreateDirectMessageCmd request, CancellationToken cancellationToken)
    {
        var messageType = await _dataLayer.MessageTypes.FirstOrDefaultAsync(i => i.Guid == $"{request.MessageType}", CancellationToken.None);
        if (messageType == null)
        {
            return new ()
            {
                Message = $"Message type with guid {request.MessageType} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }

        switch (messageType.Guid)
        {
            // Type SMS
            case "f4fca110-790d-41d7-a0be-b5c699c9a9db":
                
                break;
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };

    }
}