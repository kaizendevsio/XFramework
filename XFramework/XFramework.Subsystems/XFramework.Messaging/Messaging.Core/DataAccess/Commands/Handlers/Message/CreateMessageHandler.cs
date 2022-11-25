using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class CreateMessageHandler : CommandBaseHandler, IRequestHandler<CreateMessageCmd, CmdResponse<CreateMessageCmd>>
{
    public CreateMessageHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateMessageCmd>> Handle(CreateMessageCmd request, CancellationToken cancellationToken)
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

        var threadMember = await _dataLayer.MessageThreadMembers.FirstOrDefaultAsync(x => x.Guid == $"{request.MessageThreadMemberGuid}", CancellationToken.None);
        if (threadMember is null)
        {
            return new ()
            {
                Message = $"Message thread member with guid {request.MessageThreadMemberGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var message = request.Adapt<Domain.DataTransferObjects.Message>();
        message.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        message.MessageThread = thread;
        message.MessageThreadMember = threadMember;
        
        await _dataLayer.Messages.AddAsync(message, CancellationToken.None);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(message.Guid);
        return new()
        {
            Message = $"Message with guid {message.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}