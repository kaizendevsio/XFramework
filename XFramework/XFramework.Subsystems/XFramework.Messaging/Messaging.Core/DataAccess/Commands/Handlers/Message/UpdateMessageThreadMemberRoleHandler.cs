using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class UpdateMessageThreadMemberRoleHandler : CommandBaseHandler, IRequestHandler<UpdateMessageThreadMemberRoleCmd, CmdResponse<UpdateMessageThreadMemberRoleCmd>>
{
    public UpdateMessageThreadMemberRoleHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<UpdateMessageThreadMemberRoleCmd>> Handle(UpdateMessageThreadMemberRoleCmd request, CancellationToken cancellationToken)
    {
        var existingRole = await _dataLayer.MessageThreadMemberRoles.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingRole is null)
        {
            return new ()
            {
                Message = $"Role with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedRole = request.Adapt(existingRole);

        /*
        if (request.RoleGuid is null)
        {
            var role = await _dataLayer.MessageThreadMemberRoles.FirstOrDefaultAsync(x => x.Guid == $"{request.RoleGuid}", CancellationToken.None);
            if (role is null)
            {
                return new()
                {
                    Message = $"Role with guid {request.RoleGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedRole.Role = role;
        }
        */

        if (request.MessageThreadMemberGuid is null)
        {
            var member = await _dataLayer.MessageThreadMembers.FirstOrDefaultAsync(x => x.Guid == $"{request.MessageThreadMemberGuid}", CancellationToken.None);
            if (member is null)
            {
                return new ()
                {
                    Message = $"Message thread member with guid {request.MessageThreadMemberGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedRole.MessageThreadMember = member;
        }
        
        _dataLayer.MessageThreadMemberRoles.Update(updatedRole);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Role with Guid {request.Guid} updated",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}