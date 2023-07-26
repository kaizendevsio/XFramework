using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class CreateMessageThreadMemberHandler : CommandBaseHandler, IRequestHandler<CreateMessageThreadMemberCmd, CmdResponse<CreateMessageThreadMemberCmd>>
{
    public CreateMessageThreadMemberHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<CreateMessageThreadMemberCmd>> Handle(CreateMessageThreadMemberCmd request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.MessageThreadMemberGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.GroupGuid}", CancellationToken.None);
        if (group is null)
        {
            return new ()
            {
                Message = $"Group with guid {request.GroupGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var thread = await _dataLayer.MessageThreads.FirstOrDefaultAsync(x => x.Guid == $"{request.MessageThreadGuid}", CancellationToken.None);
        if (thread is null)
        {
            return new ()
            {
                Message = $"Thread with guid {request.MessageThreadGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var credential = await _dataLayer.IdentityCredentials.FirstOrDefaultAsync(x => x.Guid == $"{request.IdentityCredentialGuid}", CancellationToken.None);
        if (credential is null)
        {
            return new ()
            {
                Message = $"Credential with guid {request.IdentityCredentialGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var member = request.Adapt<MessageThreadMember>();
        member.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        member.MessageThread = thread;
        member.Group = group;
        member.IdentityCredential = credential;
        
        await _dataLayer.MessageThreadMembers.AddAsync(member, CancellationToken.None);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(member.Guid);
        return new()
        {
            Message = $"Message thread member with guid {member.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
        };

    }
}