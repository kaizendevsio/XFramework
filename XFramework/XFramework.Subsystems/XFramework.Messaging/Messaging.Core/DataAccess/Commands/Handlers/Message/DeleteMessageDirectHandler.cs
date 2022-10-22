using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class DeleteMessageDirectHandler : CommandBaseHandler, IRequestHandler<DeleteMessageDirectCmd, CmdResponse<DeleteMessageDirectCmd>>
{
    public DeleteMessageDirectHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteMessageDirectCmd>> Handle(DeleteMessageDirectCmd request, CancellationToken cancellationToken)
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
        
        existingDirect.IsDeleted = true;
        existingDirect.IsEnabled = false;

        _dataLayer.Update(existingDirect);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Message Direct with Guid {request.Guid} deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}