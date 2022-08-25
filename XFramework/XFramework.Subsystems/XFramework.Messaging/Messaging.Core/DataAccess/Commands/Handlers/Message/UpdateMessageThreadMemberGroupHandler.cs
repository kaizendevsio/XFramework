using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class UpdateMessageThreadMemberGroupHandler : CommandBaseHandler, IRequestHandler<UpdateMessageThreadMemberGroupCmd, CmdResponse<UpdateMessageThreadMemberGroupCmd>>
{
    public UpdateMessageThreadMemberGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<UpdateMessageThreadMemberGroupCmd>> Handle(UpdateMessageThreadMemberGroupCmd request, CancellationToken cancellationToken)
    {
        var existingGroup = await _dataLayer.MessageThreadMemberGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingGroup is null)
        {
            return new ()
            {
                Message = $"Group with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedGroup = request.Adapt(existingGroup);

        if (request.MessageThreadGuid is null)
        {
            var thread = await _dataLayer.MessageThreads.FirstOrDefaultAsync(x => x.Guid == $"{request.MessageThreadGuid}", CancellationToken.None);
            if (thread is null)
            {
                return new ()
                {
                    Message = $"Message thread with guid {request.MessageThreadGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedGroup.MessageThread = thread;
        }
        
        _dataLayer.MessageThreadMemberGroups.Update(updatedGroup);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Group with Guid {request.Guid} updated",
            HttpStatusCode = HttpStatusCode.OK
        };

    }
}