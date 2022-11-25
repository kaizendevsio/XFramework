using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class UpdateMessageFileHandler : CommandBaseHandler, IRequestHandler<UpdateMessageFileCmd, CmdResponse<UpdateMessageFileCmd>>
{
    public UpdateMessageFileHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateMessageFileCmd>> Handle(UpdateMessageFileCmd request, CancellationToken cancellationToken)
    {
        var existingFile = await _dataLayer.MessageFiles.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingFile is null)
        {
            return new ()
            {
                Message = $"File with guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedFile = request.Adapt(existingFile);

        if (request.MessageGuid is null)
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
            updatedFile.Message = message;
        }

        if (request.StorageGuid is null)
        {
            var storage = await _dataLayer.StorageFiles.FirstOrDefaultAsync(x => x.Guid == $"{request.StorageGuid}", CancellationToken.None);
            if (storage is null)
            {
                return new ()
                {
                    Message = $"Storage with guid {request.StorageGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedFile.Storage = storage;
        }
        
        _dataLayer.MessageFiles.Update(updatedFile);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"File with guid {request.Guid} updated",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}