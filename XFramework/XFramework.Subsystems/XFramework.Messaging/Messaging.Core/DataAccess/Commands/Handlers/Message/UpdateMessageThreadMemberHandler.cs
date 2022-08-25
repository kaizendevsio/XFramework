using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class UpdateMessageThreadMemberHandler : CommandBaseHandler, IRequestHandler<UpdateMessageThreadMemberCmd, CmdResponse<UpdateMessageThreadMemberCmd>>
{
    public UpdateMessageThreadMemberHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateMessageThreadMemberCmd>> Handle(UpdateMessageThreadMemberCmd request, CancellationToken cancellationToken)
    {
        var existingMember = await _dataLayer.MessageThreadMembers.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingMember is null)
        {
            return new ()
            {
                Message = $"Member with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedMember = request.Adapt(existingMember);

        if (request.GroupGuid is null)
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
            updatedMember.Group = group;
        }

        if (request.MessageThreadGuid is null)
        {
            var thread = await _dataLayer.MessageThreads.FirstOrDefaultAsync(x => x.Guid == $"{request.MessageThreadGuid}", CancellationToken.None);
            if (thread is null)
            {
                return new ()
                {
                    Message = $"Thread with guid {request.MessageThreadGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedMember.MessageThread = thread;
        }

        if (request.IdentityCredentialGuid is null)
        {
            var credential = await _dataLayer.IdentityCredentials.FirstOrDefaultAsync(x => x.Guid == $"{request.IdentityCredentialGuid}", CancellationToken.None);
            if (credential is null)
            {
                return new ()
                {
                    Message = $"Credential with guid {request.IdentityCredentialGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedMember.IdentityCredential = credential;
        }
        
        _dataLayer.MessageThreadMembers.Update(updatedMember);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Member with Guid {request.Guid} updated",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}