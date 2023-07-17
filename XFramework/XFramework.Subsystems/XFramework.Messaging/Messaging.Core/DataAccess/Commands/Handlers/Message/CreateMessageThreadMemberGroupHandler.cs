using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class CreateMessageThreadMemberGroupHandler : CommandBaseHandler, IRequestHandler<CreateMessageThreadMemberGroupCmd, CmdResponse<CreateMessageThreadMemberGroupCmd>>
{
    public CreateMessageThreadMemberGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<CreateMessageThreadMemberGroupCmd>> Handle(CreateMessageThreadMemberGroupCmd request, CancellationToken cancellationToken)
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

        var member = request.Adapt<MessageThreadMemberGroup>();
        member.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        member.MessageThread = thread;
        
        await _dataLayer.MessageThreadMemberGroups.AddAsync(member, CancellationToken.None);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(member.Guid);
        return new()
        {
            Message = $"Message thread member group with guid {member.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
        };

    }
}