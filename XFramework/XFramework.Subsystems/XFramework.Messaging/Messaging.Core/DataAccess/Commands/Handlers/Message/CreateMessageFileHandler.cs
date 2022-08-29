using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class CreateMessageFileHandler : CommandBaseHandler, IRequestHandler<CreateMessageFileCmd, CmdResponse<CreateMessageFileCmd>>
{
    public CreateMessageFileHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateMessageFileCmd>> Handle(CreateMessageFileCmd request, CancellationToken cancellationToken)
    {
        var message = await _dataLayer.Messages.FirstOrDefaultAsync(x => x.Guid == $"{request.MessageGuid}", CancellationToken.None);
        if (message is null)
        {
            return new ()
            {
                Message = $"Message with guid {request.MessageGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var storage = await _dataLayer.StorageFiles.FirstOrDefaultAsync(x => x.Guid == $"{request.StorageGuid}", CancellationToken.None);
        if (storage is null)
        {
            return new ()
            {
                Message = $"Storage with guid {request.StorageGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var file = request.Adapt<MessageFile>();
        file.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        file.Message = message;
        file.Storage = storage;
        
        await _dataLayer.MessageFiles.AddAsync(file, CancellationToken.None);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        
        request.Guid = Guid.Parse(file.Guid);
        return new()
        {
            Message = $"Message file with guid {file.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}