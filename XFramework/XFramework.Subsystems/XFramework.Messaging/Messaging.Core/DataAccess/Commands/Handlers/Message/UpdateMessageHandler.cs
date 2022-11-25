using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class UpdateMessageHandler : CommandBaseHandler, IRequestHandler<UpdateMessageCmd, CmdResponse<UpdateMessageCmd>>
{
    public UpdateMessageHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<UpdateMessageCmd>> Handle(UpdateMessageCmd request, CancellationToken cancellationToken)
    {
        var existingMessage = await _dataLayer.Messages.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingMessage is null)
        {
            return new ()
            {
                Message = $"Message with guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedMessage = request.Adapt(existingMessage);

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
            updatedMessage.MessageThread = thread;
        }

        if (request.MessageThreadMemberGuid is null)
        {
            var threadMember = await _dataLayer.MessageThreadMembers.FirstOrDefaultAsync(x => x.Guid == $"{request.MessageThreadMemberGuid}", CancellationToken.None);
            if (threadMember is null)
            {
                return new ()
                {
                    Message = $"Message thread member with guid {request.MessageThreadMemberGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedMessage.MessageThreadMember = threadMember;
        }
        
        _dataLayer.Messages.Update(updatedMessage);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Message with guid {request.Guid} updated",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}