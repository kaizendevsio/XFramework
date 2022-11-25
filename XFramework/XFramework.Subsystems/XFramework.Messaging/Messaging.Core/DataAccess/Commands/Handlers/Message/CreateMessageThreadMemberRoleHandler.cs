using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class CreateMessageThreadMemberRoleHandler : CommandBaseHandler, IRequestHandler<CreateMessageThreadMemberRoleCmd, CmdResponse<CreateMessageThreadMemberRoleCmd>>
{
    public CreateMessageThreadMemberRoleHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<CreateMessageThreadMemberRoleCmd>> Handle(CreateMessageThreadMemberRoleCmd request, CancellationToken cancellationToken)
    {
        /*var role = await _dataLayer.MessageThreadMemberRoles.FirstOrDefaultAsync(x => x.Guid == $"{request.RoleGuid}", CancellationToken.None);
        if (role is null)
        {
            return new ()
            {
                Message = $"Role with guid {request.RoleGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }*/

        var member = await _dataLayer.MessageThreadMembers.FirstOrDefaultAsync(x => x.Guid == $"{request.MessageThreadMemberGuid}", CancellationToken.None);
        if (member is null)
        {
            return new ()
            {
                Message = $"Message thread member with guid {request.MessageThreadMemberGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var memberRole = request.Adapt<MessageThreadMemberRole>();
        memberRole.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        /*memberRole.Role = role;*/
        memberRole.MessageThreadMember = member;
        
        await _dataLayer.MessageThreadMemberRoles.AddAsync(memberRole, CancellationToken.None);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(memberRole.Guid);
        return new()
        {
            Message = $"Message thread member role with guid {memberRole.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };

    }
}