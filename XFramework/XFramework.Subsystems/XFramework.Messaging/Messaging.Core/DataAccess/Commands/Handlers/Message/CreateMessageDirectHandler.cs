using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class CreateMessageDirectHandler : CommandBaseHandler, IRequestHandler<CreateMessageDirectCmd, CmdResponse<CreateMessageDirectCmd>>
{
    public CreateMessageDirectHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateMessageDirectCmd>> Handle(CreateMessageDirectCmd request, CancellationToken cancellationToken)
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

        var type = await _dataLayer.MessageTypes.FirstOrDefaultAsync(x => x.Guid == $"{request.TypeGuid}", CancellationToken.None);
        if (type is null)
        {
            return new ()
            {
                Message = $"Message type with guid {request.TypeGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var direct = request.Adapt<MessageDirect>();
        direct.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        direct.ParentMessage = parentMessage;
        direct.Type = type;
        
        await _dataLayer.MessageDirects.AddAsync(direct, CancellationToken.None);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(direct.Guid);
        return new()
        {
            Message = $"Message with guid {direct.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
        };
    }
}