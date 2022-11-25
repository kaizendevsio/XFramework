using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class DeleteMessageFileHandler : CommandBaseHandler, IRequestHandler<DeleteMessageFileCmd, CmdResponse<DeleteMessageFileCmd>>
{
    public DeleteMessageFileHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteMessageFileCmd>> Handle(DeleteMessageFileCmd request, CancellationToken cancellationToken)
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
        
        existingFile.IsDeleted = true;
        existingFile.IsEnabled = false;

        _dataLayer.Update(existingFile);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"File with guid {request.Guid} deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}